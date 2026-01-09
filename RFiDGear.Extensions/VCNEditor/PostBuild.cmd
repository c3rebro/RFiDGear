@echo off
REM ************************************************************************
REM * This is a post build command you can put in your project to copy the 
REM * .dll file to the proper place.
REM *
REM * In your project, in the Post Build Event, put the following code:
REM *          "$(ProjectDir)\postbuild.cmd" "$(ProjectName)" "$(TargetPath)"
REM ************************************************************************
MkDir "%ProgramData%\RFiDGear\Extensions"
MkDir "%ProgramData%\RFiDGear\Extensions\%1"
COPY %2 %ProgramData%\RFiDGear\Extensions\%1 /y
