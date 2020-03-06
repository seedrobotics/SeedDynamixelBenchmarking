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
        static void run_BULK_READ_benchmark(ref SerialPort port, ref List<byte> l_dyn_ids, byte b_read_startaddr, byte b_read_len, System.IO.TextWriter out_writter)
        {
            Console.WriteLine("#2 Read multiple devices using one BULK_READ command:");
            Console.WriteLine("==========================================");
            Console.WriteLine();

            check_return_delay_times(ref port, l_dyn_ids);

            if (out_writter != null)
            {
                out_writter.Write("BULK READ,Cycle Nr,");

                foreach (byte b_id in l_dyn_ids)
                {
                    out_writter.Write("ID {0},", b_id);
                }
                out_writter.WriteLine("Total Cycle time");
            }

            List<long> iteration_times = new List<long>(); int failed_runs = 0;
            long min_time_innercycle = long.MaxValue, max_time_innercycle = -1;
            byte min_time_innercycle_id = 0, max_time_innercycle_id = 0;
            long min_time_outercycle = long.MaxValue, max_time_outercycle = -1;

            Dynamixel1CommandGenerator dync = new Dynamixel1CommandGenerator();
            int bytes_received = 0; long inner_cycle_time = 0;

            StopWatchMicrosecs outer_timer = new StopWatchMicrosecs(); bool run_failed = false; int cycle_nr = 0;

            // build BULK_READ command
            // create per device parameter triplets
            List<Dynamixel1BulkReadDeviceEntry> l_devicep = new List<Dynamixel1BulkReadDeviceEntry>();
            foreach (byte b_id in l_dyn_ids)
            {
                l_devicep.Add(new Dynamixel1BulkReadDeviceEntry(b_id, b_read_startaddr, b_read_len));
            }
            byte[] command = dync.generate_bulkread_packet(l_devicep);

            for (cycle_nr = 0; cycle_nr < NUMBER_OF_CYCLES_PER_BENCHMARK; cycle_nr++)
            {
                outer_timer.Reset();
                outer_timer.Start();

                if (out_writter != null)
                {
                    out_writter.Write("BULK READ,{0},", cycle_nr);
                }

                port.Write(command, 0, command.Length);

                // get replies
                foreach (byte b_id in l_dyn_ids)
                {
                    run_failed = false;

                    byte[] reply = dync.get_dyn_reply(port, (byte)(Dynamixel1CommandGenerator.DYN1_REPLY_SZ_READ_MIN + b_read_len), 40000, ref bytes_received, ref inner_cycle_time);
                    if (reply != null)
                    {
                        if (out_writter != null) out_writter.Write("{0},", inner_cycle_time);

                        if (inner_cycle_time < min_time_innercycle)
                        {
                            min_time_innercycle = inner_cycle_time;
                            min_time_innercycle_id = b_id;
                        }

                        if (inner_cycle_time > max_time_innercycle)
                        {
                            max_time_innercycle = inner_cycle_time;
                            max_time_innercycle_id = b_id;
                        }
                    }
                    else
                    {
                        // run fails
                        run_failed = true;
                        port.ReadExisting(); // purge port
                        if (out_writter != null) out_writter.WriteLine("FAILED");
                        break;
                    }
                }
                outer_timer.Stop();

                if (!run_failed)
                {
                    min_time_outercycle = Math.Min(min_time_outercycle, outer_timer.ElapsedMicroseconds);
                    max_time_outercycle = Math.Max(max_time_outercycle, outer_timer.ElapsedMicroseconds);

                    iteration_times.Add(outer_timer.ElapsedMicroseconds);
                    if (out_writter != null) out_writter.WriteLine(outer_timer.ElapsedMicroseconds);
                }
                else
                {
                    failed_runs++;
                }

                Console.Write("\rCycle #{0}: Cycle time {1} uSec.  (Fastest so far {2} uSec; slowest so far {3} uSec. Failed runs: {4})     ", cycle_nr, (run_failed ? "FAILED" : outer_timer.ElapsedMicroseconds.ToString()), min_time_outercycle, max_time_outercycle, failed_runs);
            }


            // test cycle ended. run stats
            double avg, std_dev;
            if (iteration_times.Count > 1)
            {
                avg = iteration_times.Average();
                std_dev = Math.Sqrt(iteration_times.Average(v => Math.Pow(v - avg, 2)));
            }
            else
            {
                avg = 0; std_dev = 0;
            }


            Console.WriteLine("\rStatistics:                                                                              ");
            Console.WriteLine("\tCycles ran         : {0,5}", cycle_nr);
            Console.WriteLine("\tCycles failed      : {0,5} {1}", failed_runs, (failed_runs > 0 ? "(check if the IDs are all connected and responding)" : ""));
            Console.WriteLine("\tFastest Cycle time : {0,5} uSec", min_time_outercycle);
            Console.WriteLine("\tSlowest Cycle time : {0,5} uSec", max_time_outercycle);
            Console.WriteLine("\tAverage Cycle time : {0,5} uSec", avg);
            Console.WriteLine("\tStandard Deviation : {0,5}", std_dev);
            Console.WriteLine("\t----------------------");
            Console.WriteLine("\tFastest Individual Device: ID {0}, reply time {1,5} uSec", min_time_innercycle_id, min_time_innercycle);
            Console.WriteLine("\tSlowest Individual Device: ID {0}, reply time {1,5} uSec", max_time_innercycle_id, max_time_innercycle);
            Console.WriteLine();
        }

    }
}