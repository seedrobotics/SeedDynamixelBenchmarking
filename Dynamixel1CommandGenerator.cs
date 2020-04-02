using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;

namespace Dynamixel1Commander
{
    /* this class provides higher resolution timing
       needed for benchmarking */
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

    public class Dynamixel1BulkReadDeviceEntry
    {
        public byte dyn_id;
        public byte read_start_addr;
        public byte read_len;

        Dynamixel1BulkReadDeviceEntry() { }

        public Dynamixel1BulkReadDeviceEntry(byte b_id, byte b_read_startaddr, byte b_read_len)
        {
            this.dyn_id = b_id;
            this.read_start_addr = b_read_startaddr;
            this.read_len = b_read_len;
        }
    }

    public class Dynamixel1CommandGenerator
    {

        public const byte DYN1_INSTR_PING = 0x1;
        public const byte DYN1_INSTR_READ = 0x2;
        public const byte DYN1_INSTR_WRITE = 0x3;
        public const byte DYN1_INSTR_SYNC_WRITE = 0x83;
        public const byte DYN1_INSTR_BULK_READ = 0x92;
        public const byte DYN1_INSTR_DSYNC_WRITE = 0x84;

        public const byte DYN1_INSTR_WRITE_NO_REPLY = 0x33; // extension to DYN1 implemented in Seed Robotics fw
        public const byte DYN1_INSTR_REBOOT = 0x8; // Seed Robotics extension in DYN1; not part of DYN1 standard
        public const byte DYN1_INSTR_JUMP_TO_BOOTLDR = 0x9; // Seed Robotics extension in DYN1; not part of DYN1 standard

        public const byte ID_BROADCAST = 0xfe;


        public const byte DYN1_REPLY_SZ_READ_MIN = 6;
        public const byte DYN1_REPLY_SZ_WRITE = 6;
        public const byte DYN1_REPLY_SZ_PING = 6;

        private enum enReplyState
        {
            SEEKING_1ST_FF,
            SEEKING_2ND_FF,
            SEEKING_ID,
            SEEKING_LEN,
            SEEKING_INSTR_ERR_MASK,
            SEEKING_PARAMS,
            SEEKING_CHECKSUM,
            PACKET_COMPLETE            
        };

        public enum en_reply_result { NONE, MISTMATCHED_DEVICE_ID, INVALID_COMMAND_LEN, INVALID_CHECKSUM, TIMEOUT };
        public en_reply_result last_get_dyn_reply_error = en_reply_result.NONE;

        public byte[] generate_dyn_packet(byte ID, byte INSTRUCTION, List<byte> PARAMS)
        {
            byte checksum = 0;

            List<byte> command_bytes = new List<byte>();

            command_bytes.Add(0xff);
            command_bytes.Add(0xff);
            command_bytes.Add(ID);
            command_bytes.Add((byte)(PARAMS.Count + 2));
            command_bytes.Add(INSTRUCTION);            
            command_bytes.AddRange(PARAMS);

            for (byte b = 2; b < command_bytes.Count; b++) {
                checksum = (byte) (checksum + command_bytes[b]);
            }
            checksum = (byte) (~checksum);

            command_bytes.Add(checksum);

            return command_bytes.ToArray();
        }

        public byte[] generate_bulkread_packet(List<Dynamixel1BulkReadDeviceEntry> device_entries)
        {
            List<byte> l_params = new List<byte>();

            l_params.Add(0x0);  // first param is always 0 for whatever reason
            foreach(Dynamixel1BulkReadDeviceEntry de in device_entries)
            {
                l_params.Add(de.read_len);
                l_params.Add(de.dyn_id);
                l_params.Add(de.read_start_addr);                
            }

            return generate_dyn_packet(ID_BROADCAST, DYN1_INSTR_BULK_READ, l_params);
        }

        // Delegated SYNC_READ: extended command implemented on the USB to Serial adapter
        // Available in the USB2AX and Seed Robotics USB to Serial adapter
        public byte[] generate_dsync_read_packet(byte delegate_device_dyn_id, List<byte> device_ids_to_read, byte start_addr, byte read_length)
        {
            List<byte> l_params = new List<byte>();

            /* Per the spec, each triplet of bytes means:
                Param 1= READ START ADDR
                Param 2 = READ LEN
                Param 3 = 1st DEVICE ID
                .
                .
                Param N = N DEVICE ID
                */

            l_params.Add(start_addr);
            l_params.Add(read_length);
            foreach (byte id in device_ids_to_read)
            {
                l_params.Add(id);
            }

            return generate_dyn_packet(delegate_device_dyn_id, DYN1_INSTR_DSYNC_WRITE, l_params);
        }


        public byte[] generate_read_packet(byte ID, byte start_addr, byte read_length)
        {
            List<byte> cmd_params = new List<byte>();
            cmd_params.Add(start_addr);
            cmd_params.Add(read_length);

            return generate_dyn_packet(ID, DYN1_INSTR_READ, cmd_params);
        }

        public byte[] generate_write_packet(byte ID, byte wr_start_address, List<byte> data_to_write)
        {
            data_to_write.Insert(0, wr_start_address);

            return generate_dyn_packet(ID, DYN1_INSTR_WRITE, data_to_write);
        }

        public byte[] generate_reboot_packet(byte ID)
        {
            return generate_dyn_packet(ID, DYN1_INSTR_REBOOT, new List<byte>());
        }

        public byte[] generate_ping_packet(byte ID)
        {
            return generate_dyn_packet(ID, DYN1_INSTR_PING, new List<byte>());
        }

        public byte[] generate_jumpt_to_bootldr_packet(byte ID, int delay_ms, byte baud_rate_register_low, byte baud_rate_register_high, int jump_address)
        {
            List<byte> cmd_params = new List<byte>();
            cmd_params.Add((byte)delay_ms);
            cmd_params.Add((byte)(delay_ms >> 8));
            cmd_params.Add(baud_rate_register_low);
            cmd_params.Add(baud_rate_register_high);
            cmd_params.Add((byte)jump_address);
            cmd_params.Add((byte)(jump_address >> 8));

            return generate_dyn_packet(ID, DYN1_INSTR_JUMP_TO_BOOTLDR, cmd_params);
        }

        public int get_word_value_from_reply(byte[] reply)
        {
            byte[] params_ = get_param_array_from_reply(reply);

            if (params_ != null && params_.Length == 2)
            {
                return (int)params_[0] + ((int)params_[0] * 256);
            }
            else {
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
            else {
                return -1;
            }
        }

        public byte[] get_param_array_from_reply(byte[] reply)
        {

            // locate the header in the reply;
            // do so to trim any data that could had been left in the buffer
            bool header_found = false;
            for(byte b = 0; b < reply.Length - 1; b++)
            {
                if (reply[b] == 0xff && reply[b+1] == 0xff) // we can do b+1 without checkoing boundaries bc the for loop is b < reply.length-1
                {
                    if (b != 0) // trim the beginning of the array
                    {
                        reply = reply.Skip(b).ToArray();
                    }
                    header_found = true;
                    break;
                }
            }

            if (!header_found || reply.Length < 6) {
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

        public byte[] get_dyn_reply(SerialPort port, byte expected_id, byte expected_reply_size, long timeout_uSecs, ref int out_reply_size, ref long out_elapsed_usecs)
        {
            last_get_dyn_reply_error = en_reply_result.NONE;

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

                    switch(e_state)
                    {
                        case enReplyState.SEEKING_1ST_FF:
                            if (in_byte == 0xFF)
                            {
                                e_state = enReplyState.SEEKING_2ND_FF;
                            } else
                            {
                                Debug.Print("Discarding 0x{0:X2}", in_byte);
                                discarded_bytes++;
                            }
                            break;

                        case enReplyState.SEEKING_2ND_FF:
                            if (in_byte == 0xFF)
                            {
                                e_state = enReplyState.SEEKING_ID;
                            } else
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
                            } else
                            {
                                //Debug.Print("Reply from ID {0}", in_byte);
                                if (in_byte != expected_id)
                                {
                                    Debug.Write(string.Format("Expected ID {0}, instead received reply from ID {1}\n", expected_id, in_byte));

                                    Debug.Write(string.Format("[id received: {0}] ", in_byte));
                                    do
                                    {
                                        while (port.BytesToRead > 0) { Debug.Write(string.Format("[{0:x}] ", port.ReadByte())); }
                                    } while (timer.ElapsedMicroseconds < timeout_uSecs);
                                    timer.Stop();
                                    Debug.Write("\n");
                                    last_get_dyn_reply_error = en_reply_result.MISTMATCHED_DEVICE_ID;
                                    return null;

                                }
                                e_state = enReplyState.SEEKING_LEN;
                            }
                            break;

                        case enReplyState.SEEKING_LEN:
                            if (expected_reply_size - 4 != in_byte)
                            {
                                Debug.Write(string.Format("Dyn ID {2}: Length in packet and expected length differ: in packet {0}(+4) / expected {1}\n", in_byte, expected_reply_size, expected_id));

                                Debug.Write(string.Format("[id: {0}] [len: {1}] ", expected_id, in_byte));
                                do
                                {
                                    if (port.BytesToRead > 0) { Debug.Write(string.Format("[{0:x}] ", port.ReadByte())); }
                                } while (timer.ElapsedMicroseconds < timeout_uSecs && --in_byte > 0); // print up to the LEN chars of this reply.
                                timer.Stop();
                                Debug.Write("\n");
                                last_get_dyn_reply_error = en_reply_result.INVALID_COMMAND_LEN;
                                return null;
                            } else
                            {
                                params_to_receive = in_byte - 2;
                                e_state = enReplyState.SEEKING_INSTR_ERR_MASK;
                            }
                            break;

                        case enReplyState.SEEKING_INSTR_ERR_MASK:
                            if (params_to_receive == 0)
                            {
                                e_state = enReplyState.SEEKING_CHECKSUM;
                            } else
                            {
                                e_state = enReplyState.SEEKING_PARAMS;
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
            } while (timer.ElapsedMicroseconds < timeout_uSecs && out_reply_size < expected_reply_size);
            timer.Stop();
            out_elapsed_usecs = timer.ElapsedMicroseconds;


            if (out_elapsed_usecs > timeout_uSecs || out_reply_size < expected_reply_size) // timeout and/or not eneough bytes
            {
                last_get_dyn_reply_error = en_reply_result.TIMEOUT;
                Debug.Print("Expected Dyn ID: {3}: did not return properly: elapsed time {0}uSecs, bytes received {1}/expected {2}", out_elapsed_usecs, out_reply_size, expected_reply_size, expected_id);

                // waiting pattern to empty the buffer
                timer.Restart();

                while(timer.ElapsedMilliseconds < 50)
                {
                    if (port.BytesToRead < 0)
                    {
                        Debug.Print("Purging orphan 0x{0:X2}", in_byte);
                    }
                }

                timer.Stop();
                return null;
            }            

            // validate the checksum
            byte received_checksum = received_bytes[out_reply_size - 1];
            byte calculated_checksum = 0;
            for (byte b=2; b < expected_reply_size - 1; b++) // b=2 bc we want to start at the ID
            {
                calculated_checksum += received_bytes[b];
            }
            if ((byte)(~calculated_checksum) != received_checksum)
            {
                Debug.Print("Invalid checksum: ID {0}, received checksum 0x{1:X2}; calculated checksum from params 0x{2:X2}", received_bytes[2], received_checksum, calculated_checksum);
                return null;
            }

            return received_bytes;
        }

    }
}
