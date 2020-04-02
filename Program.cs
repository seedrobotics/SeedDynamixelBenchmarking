using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using Dynamixel1Commander;
using System.IO;
using System.Text.RegularExpressions;

namespace SeedDynamixelBenchmarking
{
    partial class Program
    {

        private enum en_TestType { Invalid, DYN1_Base, DYN1_Extended, SCW_CommEfficiency, SCW_Move, SCW_DevTests };

        private const int NUMBER_OF_CYCLES_PER_BENCHMARK = 400;

        static int Main(string[] args)
        {
            string s_port = "", s_outfile_name=""; long l_baud_bps=1000000; bool b_scan_ids=false; List<byte> l_ids_to_query = new List<byte>();
            byte b_ctb_read_start_addr = 36; byte b_ctb_read_len = 6; en_TestType e_test_type = en_TestType.Invalid; bool b_show_help = false; bool b_run_continously = false ;

            get_cmdline_params(args, ref s_port, ref l_baud_bps, ref b_scan_ids,  ref l_ids_to_query,
                               ref b_ctb_read_start_addr, ref b_ctb_read_len, ref e_test_type, ref s_outfile_name, ref b_show_help, ref b_run_continously);

            if (b_show_help)
            {
                display_help();
                return 0;
            }

            // Ensure we have minum required arguments
            s_port = s_port.Trim();
            if (s_port.Length == 0)
            {
                Console.WriteLine("Serial port not specified. Use '-h' parameter for usage instructions.");
                return -1;
            }
            if (b_scan_ids == false && l_ids_to_query.Count == 0)
            {
                Console.WriteLine("No Dynamixel IDs specified; i.e. no -scanids or -dynids listed. Use '-h' for instructions.");
                return -1;
            }
            if (e_test_type == en_TestType.Invalid)
            {
                Console.WriteLine("Invalid test type specified. Use '-h' for instructions.");
                return -1;
            }

            // open the serial port to prepare for benchmarking
            try
             {
                TextWriter out_writer = null;
                SerialPort c_port = new SerialPort(s_port, (int)l_baud_bps);
                c_port.DtrEnable = true; // use DTR transitions
                c_port.Open();

                // main program
                if (b_scan_ids)
                {
                    scan_bus_ids(ref c_port);
                }
                else
                {

                    Console.WriteLine("Running Benchmarks:");
                    Console.WriteLine("Device IDs will be queried in the following order: ");
                    foreach (byte b_id in l_ids_to_query) { Console.Write("[{0}] ", b_id); }
                    
                    Console.WriteLine();
                    Console.WriteLine();

                    if (s_outfile_name.Trim() != "")
                    {
                        out_writer = new StreamWriter(s_outfile_name, true);
                        out_writer.WriteLine();
                        out_writer.WriteLine("''''''''''' {0}", DateTime.Now.ToString());
                    }

                    long n_cycles = 0;
                    do
                    {
                        Console.WriteLine("-------------- OVERALL CYCLE {0} (total sub cycles {1})", n_cycles, n_cycles * NUMBER_OF_CYCLES_PER_BENCHMARK);

                        if (e_test_type == en_TestType.DYN1_Base || e_test_type == en_TestType.DYN1_Extended)
                        {
                            Console.WriteLine("DYNAMIXEL 1: Will read {0} bytes from the {1} IDs, using different approaches:", b_ctb_read_len, l_ids_to_query.Count);
                            Console.WriteLine("=======================================================================");

                            if (e_test_type == en_TestType.DYN1_Extended)
                            {
                                run_dyn1_dsyncread_benchmark(ref c_port, ref l_ids_to_query, b_ctb_read_start_addr, b_ctb_read_len, out_writer);
                            }
                            else
                            {
                                run_dyn1_READ_benchmark(ref c_port, ref l_ids_to_query, b_ctb_read_start_addr, b_ctb_read_len, out_writer);
                                run_dyn1_BULK_READ_benchmark(ref c_port, ref l_ids_to_query, b_ctb_read_start_addr, b_ctb_read_len, out_writer);
                            }
                        }
                        else if (e_test_type == en_TestType.SCW_Move) 
                        {
                            Console.WriteLine("SCW Protocol: Test motor movements");
                            Console.WriteLine("=======================================================================");

                        }
                        else if (e_test_type == en_TestType.SCW_CommEfficiency) 
                        {
                            Console.WriteLine("SCW Protocol: Test comm efficiency");
                            Console.WriteLine("=======================================================================");

                        } 
                        else if (e_test_type == en_TestType.SCW_DevTests)
                        {
                            Console.WriteLine("SCW Protocol: Run batch of development tests");
                            Console.WriteLine("=======================================================================");

                        }

                        n_cycles++;

                        System.Threading.Thread.Sleep(2000);
                        c_port.ReadExisting();

                    } while (b_run_continously);
                }

                if (out_writer != null)
                {
                    out_writer.Close();
                }

                c_port.Close();

                return 0;
            } catch (Exception ex)
            {
                Console.Write("> ERROR:{0}{1}", Environment.NewLine, ex.Message);
                return -2;
            }
        }

        


        static void display_help()
        {
            Console.WriteLine();
            Console.WriteLine("Seed Robotics: Dynamixel Benchmarking tool");
            Console.WriteLine("==============================================");
            Console.WriteLine("Command line options:");
            Console.WriteLine("\t-port=[port name] - serial port to use");
            Console.WriteLine("\t-baud=[baud in bps] - baud rate to use; default=1000000/1Mbps");
            Console.WriteLine("\t-scanids - scans the bus for IDs in the varios possible protocols and exits");
            Console.WriteLine("\t-deviceids =[id1,[id2],...] - list of IDs to use for the test, separed by semi-colon");
            Console.WriteLine("\t-ctbaddr   =[start address] - DYNAMIXEL only: Control table address to start reading params; default=36");
            Console.WriteLine("\t-ctbreadlen=[read length]   - DYNAMIXEL only: Number of parameters to read from control table; default=6");
            Console.WriteLine("\t-test      = type of benchmark tests to execute, from the possible list of options below");
            Console.WriteLine("\t             dyn1_base:     Dynamixel 1 READ and BULK_READ benchmarking");
            Console.WriteLine("\t             dyn1_extended: Dynamixel 1 DSYNC_READ (Richard iBotson's implementation, Dynamixel 1 only)");
            Console.WriteLine("\t             scw_comms:     SCW protocol, test communication efficiency (and frame rate)");
            Console.WriteLine("\t             scw_move:      SCW protocol, test motor movement commands");
            Console.WriteLine("\t             scw_devtests:  SCW protocol, extensively test reading and writing device mem positions");
            Console.WriteLine("\t-outfile   =[filename] - Output individual test timings in CSV format");
            Console.WriteLine("\t-continuous - Run the test package continuously (i.e. will only stop with Ctrl+C)");
            Console.WriteLine("\t-h - this help screen");
        }

        static void get_cmdline_params(string[] args, ref string port, ref long baud_bps, ref bool scan_ids, ref List<byte> dyn_ids_to_query,
                                ref byte ctb_read_start_addr, ref byte ctb_read_len, ref en_TestType test_type, ref string outfile_name, ref bool show_help, ref bool b_run_continuously)
        {
            test_type = en_TestType.DYN1_Base; // initialize defaults; other default would have been initialized before being passed ByRef

            foreach (string arg in args)
            {
                string[] arg_params = arg.Split('=');

                if (arg_params.Length == 1)
                {
                    switch (arg_params[0].ToLower())
                    {
                        case "-h":
                        case "-?":
                            show_help = true;
                            break;

                        case "-scanids":
                            scan_ids = true;
                            break;

                        case "-continuous":
                            b_run_continuously = true;
                            break;

                        default:
                            Console.WriteLine("Invalid parameter: {0}", arg_params[0]);
                            show_help = true;
                            break;
                    }
                }
                else if (arg_params.Length == 2)
                {
                    switch (arg_params[0].ToLower())
                    {
                        case "-port":
                            port = arg_params[1];
                            break;

                        case "-baud":
                            if (long.TryParse(arg_params[1], out baud_bps) == false)
                            {
                                Console.WriteLine("Invalid baud parameter: {0}", arg);
                                show_help = true;
                            }
                            break;

                        case "-ctbaddr":
                            if (byte.TryParse(arg_params[1], out ctb_read_start_addr) == false)
                            {
                                Console.WriteLine("Invalid Read Start Address parameter: {0}", arg);
                                show_help = true;
                            }
                            break;

                        case "-ctbreadlen":
                            if (byte.TryParse(arg_params[1], out ctb_read_len) == false)
                            {
                                Console.WriteLine("Invalid Read Length parameter: {0}", arg);
                                show_help = true;
                            }
                            break;

                        case "-test":
                            if (arg_params[1] == "dyn1_base")
                            {
                                test_type = en_TestType.DYN1_Base;
                            }
                            else if (arg_params[1] == "dyn1_extended")
                            {
                                test_type = en_TestType.DYN1_Extended;
                            }
                            else if (arg_params[1] == "scw_comms")
                            {
                                test_type = en_TestType.SCW_CommEfficiency;
                            }
                            else if (arg_params[1] == "scw_move")
                            {
                                test_type = en_TestType.SCW_Move;
                            }
                            else if (arg_params[1] == "scw_devtests")
                            {
                                test_type = en_TestType.SCW_DevTests;
                            }
                            else
                            {
                                test_type = en_TestType.Invalid;
                                Console.WriteLine("Invalid Test type: {0}", arg);
                                show_help = true;
                            }
                            break;

                        case "-deviceids":
                            // split the parameter by commas and add them to the list
                            string[] s_dynids = arg_params[1].Split(',');
                            dyn_ids_to_query.Clear();

                            byte b_dyn_id;
                            foreach (string s_dynid in s_dynids)
                            {
                                if (byte.TryParse(s_dynid, out b_dyn_id))
                                {
                                    dyn_ids_to_query.Add(b_dyn_id);
                                }
                                else
                                {
                                    Console.WriteLine("Invalid ID specified in parameter: {0}, offending ID: '{1}'", arg_params[0], s_dynid);
                                    show_help = true;
                                }
                            }
                            break;

                        case "-outfile":
                            if (arg_params[1].Trim().Length == 0)
                            {
                                Console.WriteLine("You must specify the name of file, after the {0} parameter.", arg_params[0]);
                                show_help = true;
                            } else { 
                                Regex containsABadCharacter = new Regex("["
                                        + Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars())) + "]");
                                if (containsABadCharacter.IsMatch(arg_params[1]))
                                {
                                    Console.WriteLine("The filename specified for the {0} parameter is invalid.", arg_params[0]);
                                } else
                                {
                                    outfile_name = arg_params[1];
                                }
                            }

                            break;

                        default:
                            Console.WriteLine("Invalid parameter: {0}", arg_params[0]);
                            show_help = true;
                            break;
                    }

                }
                else {
                    Console.WriteLine("Invalid parameter: {0}", arg);
                    show_help = true;
                    break;
                }
            }
        }
    }
}
