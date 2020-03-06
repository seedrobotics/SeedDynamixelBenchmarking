# SeedDynamixelBenchmarking

Communication Diaignostics and Benchmarking tool for Seed Robotics Hands

This a .Net tool and should compile with Mono (on Linux/Windows) and VS (C#) on Windows as well.

We recommend beginning by benchmarking against the main board ID (usually 30 or 40 - OR - 21 or 22 in the case of RH4D)
You can then add more Device IDs as you go along.

Start by connecting the unit to your computer and powering it up.

A typical command would be:

SeedDynamixelBenchmarking.exe -port=[your comm port, i.e. COM9 or /dev/tty...] -dynids=[main board ID, for example 30] -test=base


IMPORTANT NOTES:

If you're using our supplied cable, which is based on FTDI chip, you can GREATLY improve performance by configuring its latency to 1ms.

Google "configure ftdi latency [your O/S]" and you'll find plenty of information about this on the web.



