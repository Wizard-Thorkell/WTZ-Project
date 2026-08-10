@echo off

call git submodule update --init --recursive
call dotnet build SpaceStation14.slnx -c Debug

pause
