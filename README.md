# SeedDynamixelBenchmarking

Communication Diagnostics and Benchmarking tool for Seed Robotics Hands (Dynamixel protocol) and Actuators ( SCW protocol - firmare v70 and above).

## Compiling and Running

This a .Net tool and should compile with Mono (on Linux/Windows), and also VS (C#) on Windows.
Start by connecting the unit to your computer and powering it up.


## Dynamixel Protocol Benchmarking

Dynamixel Protocol is used to control and query the whole manipulator or hand. Therefore, the main connector at the back of the hand should be used (i.e. the 3 or 4 pin connector)

We recommend beginning by benchmarking against the main board ID (usually 30 or 40 - OR - 21 or 22 in the case of RH4D)
You can then add more Device IDs as you go along, using the '-deviceids' parameter.

A typical command would be:

    SeedDynamixelBenchmarking.exe -port=[your comm port, i.e. COM9 or /dev/tty...] -deviceids=[main board ID, for example 30] -test=dyn1_base

**IMPORTANT**:

If you're using our supplied cable, which is based on FTDI chip, you can GREATLY improve performance by configuring its latency to 1ms.
Google "configure ftdi latency [your O/S]" and you'll find plenty of information about this on the web.



## SCW (Seed Robotics Continuous floW) protocol 

SCW is used by the internal actuators as part of the new *Centimani* upgrade. Benchmarking the SCW protocol can only
be performed with a direct connection to the actuators; this is meant for use in development and diagnostics by the Seed Robotics team.

    SeedDynamixelBenchmarking.exe -port=[your comm port, i.e. COM9 or /dev/tty...] -deviceids=[list of actuator IDs] -test=scw_nomotion
