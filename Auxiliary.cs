using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using DynamixelCommander;

namespace SeedDynamixelBenchmarking
{
    partial class Program
    {
        private const byte ADDR_RETURN_DELAY_TIME = 5;

        static void check_return_delay_times(ref SerialPort port, List<byte> dyn_ids)
        {
            Dynamixel1CommandGenerator cg = new Dynamixel1CommandGenerator();

            int reply_size = 0; long reply_time = 0;
            foreach(byte dyn_id in dyn_ids) {
                byte[] packet = cg.generate_read_packet(dyn_id, ADDR_RETURN_DELAY_TIME, 1);

                port.Write(packet, 0, packet.Length);

                byte[] reply = cg.get_dyn_reply(port, Dynamixel1CommandGenerator.DYN1_REPLY_SZ_READ_MIN + 1, 40000, ref reply_size, ref reply_time);

                if (reply == null)
                {
                    Console.WriteLine("ERROR: Unable to check Return Delay time configuration for ID {0} (either no reply or reply was not as expected)", dyn_id);
                } else
                {
                     int ret_delay_time = cg.get_byte_value_from_reply(reply);

                    if (ret_delay_time < 0)
                    {
                        Console.WriteLine("ERROR: Unable to check Return Delay time configuration for ID {0} (unable to parse reply)", dyn_id);
                    } else if (ret_delay_time != 0)
                    {
                        Console.WriteLine("WARNING: Device with ID {0} is not configured for optimal performance. Return Delay time set to {1}({2}ms); for best performance set this to 0.", dyn_id, ret_delay_time, ret_delay_time*2);
                    }
                }
            }
        }


        static void scan_bus_ids(ref SerialPort port)
        {
            Console.WriteLine();
            Console.WriteLine("Scanning Bus for Device IDs");
            Console.WriteLine("on serial port: {0} at {1} bps", port.PortName, port.BaudRate);
            Console.WriteLine("============================================");
            Console.WriteLine(">Only the scan will be performed; if you specified other actions, they will be ignored.");
            Console.WriteLine(">To run an actual benchmark test, explicitly list the Dynamixel IDs to use via the -dynids parameter.");
            Console.WriteLine();

            Dynamixel1CommandGenerator dync = new Dynamixel1CommandGenerator();

            int bytes_received = 0; long reply_time_usecs = 0, reply_time_ticks = 0;
            for (byte b = 0; b < Dynamixel1CommandGenerator.ID_BROADCAST; b++)
            {
                Console.Write("\rScanning ID {0} ...", b);

                byte[] dyn_command = dync.generate_ping_packet(b);

                port.Write(dyn_command, 0, dyn_command.Length);

                byte[] reply = dync.get_dyn_reply(port, Dynamixel1CommandGenerator.DYN1_REPLY_SZ_PING, 20000, ref bytes_received, ref reply_time_usecs);
                if (reply != null)
                {
                    Console.WriteLine("\rFound ID: {0,3}, replied in {1,5} uSecs)", b, reply_time_usecs, reply_time_ticks);
                    check_return_delay_times(ref port, new List<byte> { b });
                }
            }

            Console.WriteLine("\rDone                                      ");
        }

    }
}
