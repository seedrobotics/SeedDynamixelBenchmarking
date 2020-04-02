using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Ports;

namespace SeedDynamixelBenchmarking
{
    public class StopWatchMicrosecs : Stopwatch
    {
        public long ElapsedMicroseconds
        {
            get
            {
                // Stopwatch.Frequency returns the number of ticks per second; thus dividing ticks
                // per the frequency as a double, to keep precision, and then multiplying by 1000 000 gives you Microsecs
                return (long)(((double)base.ElapsedTicks / (double)Stopwatch.Frequency) * 1000000.0);
            }
        }
    }

    class SCWCommandGenerator {
        // Serial commands

        /* Command format:
         Formato Pacote de WRITE e COMMIT EEPROM
            FF FF [ID] [LEN] [COMMANDO] [WR Bitmask 1] [WR Bitmask 2] [PARAMS] [SEND_ACK] [CHECKSUM]
            -  PARAMS deve ter tamanho da zona amarela abaixo; a bitmask indicará quais desses bytes são escritos e quais são ignorados
            - LEN será  NR PARAMS + 5 (COMMAND + WR BITMS1 + WR BITMASK2 + SEND_ACK + CHECKSUM)

         Formato Pacote de GET (Read)
            FF FF [ID] [LEN] [COMMANDO] [CHECKSUM]
            - LEN será sempre 2

         Formato Pacote de Resposta (ACK)
            FF FF [ID] [LEN] 0x50(=ACK) [PARAMS][CHECKSUM]
            - LEN será NR PARAMS + 1
         
         * ACK simples a um Write (quando pedido ACK no comando) - OU - NACK (WRITE com ACK pedido ou READ)
            FF FF [ID] [LEN=0x2] 0x50/0x51(=ACK/NACK) [CHECKSUM]

         Evitar Confusoes com dispositivos Dyn1 no mesmo bus
            - Funcionar a 500kbps FIXOS
            - Checksum = ~(ID + commando +[  wr.Bitmask 1 + Wr.Bitmask 2] + Params + SEND ACK + 0x22) - ao somar 0x22 (troco 2 nibbles), o checksum fica invalido para um comando DYN1

         */

        public const byte SCMD_GET_EEPROM = 0x34;
        public const byte SCMD_GET_SRAM = 0x35;
        public const byte SCMD_WRITE_EEPROM = 0x36;
        public const byte SCMD_WRITE_SRAM = 0x37;
        public const byte SCMD_BURN_EEPROM = 0x40;
        public const byte SCMD_ACK_REPLY = 0x50;
        public const byte SCMD_NACK = 0x51;

        public const byte SEEPROM_MAP_SIZE = 0x15;
        public const byte SRAM_MAP_SIZE = 0x13;

        public const byte SCMD_REPLY_SIZE_ACK_NACK = 6;
        public const byte SCMD_REPLY_SIZE_GET_EEPROM = 6 + SEEPROM_MAP_SIZE;
        public const byte SCMD_REPLY_SIZE_GET_SRAM = 6 + SRAM_MAP_SIZE;

        // EEPROM Map;
        public const byte SEP_DEVICE_ID = 0x0;
        public const byte SEP_ZERO_OFFSET = 0x1;
        public const byte SEP_POT_LIMIT_CW_LOW = 0x2;
        public const byte SEP_POT_LIMIT_CW_HIGH = 0x3;
        public const byte SEP_POT_LIMIT_CCW_LOW = 0x4;
        public const byte SEP_POT_LIMIT_CCW_HIGH = 0x5;
        public const byte SEP_TEMP_OFFSET = 0x6;
        public const byte SEP_SPEED_GAIN_P = 0x7;
        public const byte SEP_SPEED_GAIN_I = 0x8;
        public const byte SEP_POS_GAIN_P = 0x9;
        public const byte SEP_POS_GAIN_I = 0xA;
        public const byte SEP_POS_GAIN_D = 0xB;
        public const byte SEP_PWM_PRES_TCCR1A = 0xC;
        public const byte SEP_PWM_PRES_TCCR1B = 0xD;
        public const byte SEP_MODEL_NR = 0xE;
        public const byte SEP_FW_VERSION = 0xF;
        public const byte SEP_JOINT_POSITION = 0x10;
        public const byte SEP_BOOTLDR_TIMEOUT = 0x11;
        public const byte SEP_BOOTLDR_PWLEN = 0x12;
        public const byte SEP_BOOTLDR_PWFIRST_CHAR = 0x13;
        public const byte SEP_BOOTLDR_JOINT_POS = 0x14;

        // RAM MAP;
        public const byte SRAM_CONFIGURED_ERROR_MASK = 0x0;
        public const byte SRAM_PRESENT_ERROR_MASK = 0x1;
        public const byte SRAM_OP_MODE = 0x2;
        public const byte SRAM_TARGET_POT_LOW = 0x3;
        public const byte SRAM_TARGET_POT_HIGH = 0x4;
        public const byte SRAM_TARGET_SPEED_LOW = 0x5;
        public const byte SRAM_TARGET_SPEED_HIGH = 0x6;
        public const byte SRAM_TARGET_CURRENT_LOW = 0x7;
        public const byte SRAM_TARGET_CURRENT_HIGH = 0x8;
        public const byte SRAM_PRESENT_CURRENT_LOW = 0x9;
        public const byte SRAM_PRESENT_CURRENT_HIGH = 0xA;
        public const byte SRAM_PRESENT_CURRENT_WTD_CNTR = 0xB;
        public const byte SRAM_PWM_LOW = 0xC;
        public const byte SRAM_PWM_HIGH = 0xD;
        public const byte SRAM_PRESENT_POS_LOW = 0xE;
        public const byte SRAM_PRESENT_POS_HIGH = 0xF;
        public const byte SRAM_PRESENT_SPEED_LOW = 0x10;
        public const byte SRAM_PRESENT_SPEED_HIGH = 0x11;
        public const byte SRAM_PRESENT_TEMP = 0x12;

        // ERROR Flags - we use same as Dyn1, for ease of use;
        public const byte SEF_ERROR_NONE = 0x0;
        public const byte SEF_ERROR_VOLTAGE = 0x1;
        public const byte SEF_WRONG_HW_MODEL = 0x2;
        public const byte SEF_ERROR_TEMPERATURE = 0x4;
        public const byte SEF_ERROR_RANGE = 0x8;
        public const byte SEF_ERROR_CHECKSUM = 0x10;
        public const byte SEF_ERROR_OVERLOAD = 0x20;
        public const byte SEF_ERROR_INSTRUCTION = 0x40;

        public const byte SEF_DEFAULT_ERROR_MASK = 0x7E /* all except VOLTAGE */;
        public const byte SEF_PERSISTENT_ERROR_MASK = 0x5A /* masks that can't be disabled or cleared in software */;

        // OPERATION modes;
        public const byte SOP_TORQUE_DISABLED = 0x0;
        public const byte SOP_EEP_CALLIB_ENABLED = 0x1;
        public const byte SOP_POS_SPEED_CTRL_MODE = 0x2;
        public const byte SOP_DIRECT_PWM_MODE = 0x4;
        public const byte SOP_CURRENT_POS_CTRL_MODE = 0x8;

        private enum enReplyState
        {
            SEEKING_1ST_FF,
            SEEKING_2ND_FF,
            SEEKING_ID,
            SEEKING_LEN,
            SEEKING_COMMAND,
            SEEKING_PARAMS,
            SEEKING_CHECKSUM,
            PACKET_COMPLETE
        };

        public enum en_reply_result { NO_ERROR, MISTMATCHED_DEVICE_ID, INVALID_COMMAND_LEN, NACK_RECEIVED, NEITHER_ACK_NOR_NACK, INVALID_CHECKSUM, TIMEOUT };

        public en_reply_result last_comm_result = en_reply_result.NO_ERROR;

        public byte[] generate_SCW_packet(byte ID, byte INSTRUCTION, List<byte> PARAMS)
        {
            byte checksum = 0;

            List<byte> command_bytes = new List<byte>();

            command_bytes.Add(0xff);
            command_bytes.Add(0xff);
            command_bytes.Add(ID);
            command_bytes.Add((byte)(PARAMS.Count + 2));
            command_bytes.Add(INSTRUCTION);
            command_bytes.AddRange(PARAMS);

            for (byte b = 2; b < command_bytes.Count; b++)
            {
                checksum = (byte)(checksum + command_bytes[b]);
            }

            checksum = (byte)(checksum + 0x22); // extra step for SCW checksums
            checksum = (byte)(~checksum);

            command_bytes.Add(checksum);

            return command_bytes.ToArray();
        }

       
        public byte[] generate_GET_EEPROM_packet(byte ID)
        {
            List<byte> cmd_params = new List<byte>();
            return generate_SCW_packet(ID, SCMD_GET_EEPROM, cmd_params);
        }

        public byte[] generate_GET_SRAM_packet(byte ID)
        {
            List<byte> cmd_params = new List<byte>();
            return generate_SCW_packet(ID, SCMD_GET_SRAM, cmd_params);
        }

        public byte[] generate_WRITE_EEPROM_packet(byte ID, ushort wr_bitmask, List<byte> wr_params, bool request_ACK)
        {
            byte wr_bitmask1 = (byte)(wr_bitmask % 256);
            byte wr_bitmask2 = (byte)(wr_bitmask / 256);

            wr_params.InsertRange(0, new List<byte>() {wr_bitmask1, wr_bitmask2});
            wr_params.Concat(request_ACK ? new List<byte>() { 1 } : new List<byte>() { 0 }); // append at end

            return generate_SCW_packet(ID, SCMD_WRITE_EEPROM, wr_params);
        }

        public byte[] generate_WRITE_SRAM_packet(byte ID, ushort wr_bitmask, List<byte> wr_params, bool request_ACK)
        {
            byte wr_bitmask1 = (byte)(wr_bitmask % 256);
            byte wr_bitmask2 = (byte)(wr_bitmask / 256);

            wr_params.InsertRange(0, new List<byte>() { wr_bitmask1, wr_bitmask2 });
            wr_params.Concat(request_ACK ? new List<byte>() { 1 } : new List<byte>() { 0 }); // append at end

            return generate_SCW_packet(ID, SCMD_WRITE_SRAM, wr_params);
        }

        public int get_word_value_from_reply(byte[] reply)
        {
            byte[] params_ = get_param_array_from_reply(reply);

            if (params_ != null && params_.Length == 2)
            {
                return (int)params_[0] + ((int)params_[0] * 256);
            }
            else
            {
                return -1;
            }
        }

        public int get_byte_value_from_reply(byte[] reply)
        {
            byte[] params_ = get_param_array_from_reply(reply);

            if (params_ != null && params_.Length == 1)
            {
                return (int)params_[0];
            }
            else
            {
                return -1;
            }
        }

        public byte[] get_param_array_from_reply(byte[] reply)
        {

            // locate the header in the reply;
            // do so to trim any data that could had been left in the buffer
            bool header_found = false;
            for (byte b = 0; b < reply.Length - 1; b++)
            {
                if (reply[b] == 0xff && reply[b + 1] == 0xff) // we can do b+1 without checkoing boundaries bc the for loop is b < reply.length-1
                {
                    if (b != 0) // trim the beginning of the array
                    {
                        reply = reply.Skip(b).ToArray();
                    }
                    header_found = true;
                    break;
                }
            }

            if (!header_found || reply.Length < 6)
            {
                return null;
            }

            // see if packet is complete
            int packet_len = (int)reply[3];

            if (reply.Length < packet_len + 4)
            {
                return null;
            }

            int nr_params = (int)reply[3] - 2;

            byte[] out_array = new byte[nr_params];
            Array.Copy(reply, 5, out_array, 0, nr_params);

            return out_array;
        }

        public byte[] get_SCW_reply(SerialPort port, byte expected_id, byte expected_reply_size, long timeout_uSecs, ref int out_reply_size, ref long out_elapsed_usecs)
        {
            last_comm_result = en_reply_result.NO_ERROR;

            StopWatchMicrosecs timer = new StopWatchMicrosecs();

            timer.Start();

            byte[] received_bytes = new byte[expected_reply_size];
            int params_to_receive = 0; byte in_byte = 0;
            out_reply_size = 0; int discarded_bytes = 0;

            enReplyState e_state = enReplyState.SEEKING_1ST_FF;

            do
            {
                if (port.BytesToRead > 0)
                {
                    // reset timer to cope with replies that send bytes ever so sparsely
                    timer.Restart();
                    in_byte = (byte)port.ReadByte();

                    switch (e_state)
                    {
                        case enReplyState.SEEKING_1ST_FF:
                            if (in_byte == 0xFF)
                            {
                                e_state = enReplyState.SEEKING_2ND_FF;
                            }
                            else
                            {
                                Debug.Print("Discarding 0x{0:X2}", in_byte);
                                discarded_bytes++;
                            }
                            break;

                        case enReplyState.SEEKING_2ND_FF:
                            if (in_byte == 0xFF)
                            {
                                e_state = enReplyState.SEEKING_ID;
                            }
                            else
                            {
                                e_state = enReplyState.SEEKING_1ST_FF;
                            }
                            break;

                        case enReplyState.SEEKING_ID:
                            if (in_byte == 0xFF)
                            {
                                Debug.Print("Extra 0xFF received in the header; ignoring", in_byte);
                                // sequence of 0xFFs.. keep waiting until the last one
                                continue; // do not store in the receive buffer
                            }
                            else
                            {
                                //Debug.Print("Reply from ID {0}", in_byte);
                                if (in_byte != expected_id)
                                {
                                    Debug.Write(string.Format("Expected ID {0}, instead received reply from ID {1}\n", expected_id, in_byte));
                                    last_comm_result = en_reply_result.MISTMATCHED_DEVICE_ID;

                                }
                                e_state = enReplyState.SEEKING_LEN;
                            }
                            break;

                        case enReplyState.SEEKING_LEN:
                            if (expected_reply_size - 4 != in_byte && in_byte != SCMD_REPLY_SIZE_ACK_NACK)
                            {
                                Debug.Write(string.Format("SCW ID {2}: Length in packet and expected length differ: in packet {0}(+4) / expected {1}\n", in_byte, expected_reply_size, expected_id));
                                last_comm_result = en_reply_result.INVALID_COMMAND_LEN;
                            }
                            else
                            {
                                params_to_receive = in_byte - 2;
                                e_state = enReplyState.SEEKING_COMMAND;
                            }
                            break;

                        case enReplyState.SEEKING_COMMAND:
                            if (in_byte == SCMD_ACK_REPLY)
                            {
                                if (params_to_receive == 0)
                                {
                                    e_state = enReplyState.SEEKING_CHECKSUM;
                                }
                                else
                                {
                                    e_state = enReplyState.SEEKING_PARAMS;
                                }
                            }
                            else if (in_byte == SCMD_NACK)
                            {
                                Debug.Write(string.Format("SCW ID {0}: NACK received", expected_id));
                                last_comm_result = en_reply_result.NACK_RECEIVED; // to end cycle
                            }
                            else
                            {
                                Debug.Write(string.Format("SCW ID {0}: NEITHER ACK not NACK received: command received 0x{1:x}\n", expected_id, in_byte));
                                last_comm_result = en_reply_result.NEITHER_ACK_NOR_NACK; // to end cycle
                            }
                            break;

                        case enReplyState.SEEKING_PARAMS:
                            if (--params_to_receive == 0)
                            {
                                e_state = enReplyState.SEEKING_CHECKSUM;
                            }
                            // else keep accumulating parameters
                            break;

                        case enReplyState.SEEKING_CHECKSUM:
                            e_state = enReplyState.PACKET_COMPLETE;
                            break;
                    }

                    received_bytes[out_reply_size++] = in_byte; // store in buffer

                    if (e_state == enReplyState.PACKET_COMPLETE)
                    {
                        break;
                    }
                }
            } while (timer.ElapsedMicroseconds < timeout_uSecs && out_reply_size < expected_reply_size && last_comm_result == en_reply_result.NO_ERROR);
            timer.Stop();
            out_elapsed_usecs = timer.ElapsedMicroseconds;


            if ( ( out_elapsed_usecs > timeout_uSecs || out_reply_size < expected_reply_size )
                && last_comm_result == en_reply_result.NO_ERROR) {
                last_comm_result = en_reply_result.TIMEOUT;
                Debug.Print("SCW ID: {3}: TIMED OUT/INCOMPLETE REPLY - elapsed time {0}uSecs, bytes received {1}/expected {2}", out_elapsed_usecs, out_reply_size, expected_reply_size, expected_id);
            }

            if (last_comm_result != en_reply_result.NO_ERROR)
            {
                // waiting pattern to empty the buffer
                timer.Restart();

                Debug.Print("Purging orphans from buffer: ");
                Debug.Write(string.Format("[id: {0}] ", expected_id));

                while (timer.ElapsedMicroseconds < timeout_uSecs + 40000)
                {
                    if (port.BytesToRead < 0)
                    {
                        Debug.Print(" 0x{0:X2}", in_byte);
                    }
                }

                Debug.Print("\n");
                timer.Stop();
                return null;
            }

            // validate the checksum
            byte received_checksum = received_bytes[out_reply_size - 1];
            byte calculated_checksum = 0;
            for (byte b = 2; b < expected_reply_size - 1; b++) // b=2 bc we want to start at the ID
            {
                calculated_checksum += received_bytes[b];
            }
            calculated_checksum += 0x22; // extra sum, flipping 2 nibbles, to have different checksum from Dyn1 
            calculated_checksum = (byte)(~calculated_checksum);

            if (calculated_checksum != received_checksum)
            {
                Debug.Print("Invalid checksum: ID {0}, received checksum 0x{1:X2}; calculated checksum from params 0x{2:X2}", received_bytes[2], received_checksum, calculated_checksum);
                last_comm_result = en_reply_result.INVALID_CHECKSUM;
                return null;
            }

            return received_bytes;
        }

    }
}
