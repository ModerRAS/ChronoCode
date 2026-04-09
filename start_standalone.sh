#!/bin/bash
export PATH=$PATH:/root/.dotnet
cd /mnt/sdb/WorkSpace/CSharp/ChronoCode/ChronoCode
nohup dotnet run > /tmp/backend.log 2>&1 &
