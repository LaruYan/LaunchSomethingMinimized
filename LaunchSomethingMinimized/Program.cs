using System;
using System.Reflection;
using System.Text;

namespace LaunchSomethingMinimized
{
    class Program
    {

#if DEBUG
        private const bool IS_DEBUG = true;
#else
        private const bool IS_DEBUG = false;
#endif
        private const bool IF_DEPRECATED_HACK = false;

        private const String STR_INDENT = "    ";

        private const String STR_CMD_LAUNCH = "/LAUNCH";
        private const String STR_CMD_WAIT = "/WAIT";
        private const String STR_CMD_DEFAULT = STR_CMD_LAUNCH;

        static void Main(string[] args)
        {
            // 종료시 메시지를 표시하기 위한 스트링빌더입니다.
            StringBuilder sbLog = new StringBuilder();

            // 본 프로그램은 조용히 실행시키는게 목적이므로 오류가 없는 한 조용히 넘기려고 합니다.
            // 오류가 잡히면 메시지창을 표시하게 하고자 합니다.
            bool hasError = false;

            //디버그 컴파일 상태인 경우 안내 문구를 추가합니다.
            if (IS_DEBUG)
            {
                sbLog.AppendLine("Program compiled in DEBUG mode. MessageBox will appear everytime.");
                hasError = true; // 또한 메시지박스가 무조건 표시되도록 합니다.
            }

            //if (IS_DEBUG)
            //{
            //    uint exitCode = 0xF8A432EB;// (-123456789)를 32비트 안에 표현
            //
            //    int intExitCode = 1<<(32-1) | (int)(exitCode & 0x7FFFFFFF);
            //
            //    sbLog.AppendLine(intExitCode);
            //}

            if (args.Length <= 0)
            {
                //오류가 있습니다.
                hasError = true;
                //아무 명령도 오지 않았으므로 도움말을 표시합니다.
                sbLog.AppendLine(getHelp("There is nothing to do."));
            }
            else
            {
                int nCmdStart = 0;

                bool haveToWaitForFinish = STR_CMD_WAIT.Equals(STR_CMD_DEFAULT);

                if (args[nCmdStart].StartsWith("/"))
                {
                    // 첫 매개변수가 /로 시작합니다. LAUNCH인지 WAIT인지 살핍니다.
                    String cmdToLaunchOrWait = args[nCmdStart];

                    // StringComparison.OrdinalIgnoreCase는 문화적 차이나 지역화에 신경쓰지 않는 비교방식을 채용합니다.
                    if (STR_CMD_LAUNCH.Equals(cmdToLaunchOrWait, StringComparison.OrdinalIgnoreCase))
                    {
                        haveToWaitForFinish = false;
                    }
                    else if (STR_CMD_WAIT.Equals(cmdToLaunchOrWait, StringComparison.OrdinalIgnoreCase))
                    {
                        haveToWaitForFinish = true;
                    }
                    else
                    {
                        //명령어가 하나도 맞지 않는 경우 기본 명령으로 회귀.
                        sbLog.AppendLine("WARNING: No matching commands found for " + cmdToLaunchOrWait + ". defaulting to " + STR_CMD_DEFAULT );
                    }

                    // 다음 매개변수로 진행합니다.
                    nCmdStart++;
                }
                else
                {
                    // 첫 매개변수가 /로 시작하지 않습니다. 그대로 사용합니다.
                    // 기본값은 STR_CMD_DEFAULT에 정의되어 있습니다.
                }

                // 현재 매개변수 위치 이후의 매개변수, 실제로 실행할 명령만 셉니다.
                sbLog.AppendLine("# of args to launch: " + (args.Length - nCmdStart));

                if (args.Length >= 1 + nCmdStart)
                {
                    //매개변수로 전달받은 프로그램의 이름입니다.
                    String strTargetProgramName = args[0 + nCmdStart];

                    if ( ! ( strTargetProgramName.Contains(@":\") || strTargetProgramName.Contains(":/") ) )
                    {
                        //만약 절대 경로가 아니라면 절대 경로로 변환해줍니다.
                        //http://stackoverflow.com/a/4796339

                        //현재 프로세스의 경로를 활용합니다.
                        string strProcessPath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                        string strAbsoluteProgramPath = System.IO.Path.Combine(strProcessPath, strTargetProgramName);
                        strTargetProgramName = System.IO.Path.GetFullPath((new Uri(strAbsoluteProgramPath)).LocalPath);
                    }

                    //이 프로그램의 매개변수입니다.
                    String strArgsToHandover = "";

                    //프로그램을 실행시킬 준비를 합니다.
                    if (IF_DEPRECATED_HACK)
                    {
                        // TODO: 히든모드야 누구든 잘하고 잘되지만, PREVENT FOCUS UTIL의 경우 WaitForExit이 가능할지 못할지는 모르겠다.. ㅠㅠ
                        System.Diagnostics.ProcessStartInfo targetProgram = new System.Diagnostics.ProcessStartInfo(strTargetProgramName);
                        targetProgram.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    
                    
                        sbLog.AppendLine("Preparing to launch in minized mode: " + strTargetProgramName);

                        if (args.Length >= 2 + nCmdStart)
                        {
                            // 매개변수의 갯수가 2개 이상인경우 다음 매개변수를 모두 합칩니다.
                            StringBuilder sbArgs = new StringBuilder();
                            for (int cur = 1 + nCmdStart; cur < args.Length; cur++)
                            {
                                //첫번째가 아닌 경우 사이에 빈 칸을 넣어줍니다.
                                if (cur != 1 + nCmdStart)
                                {
                                    sbArgs.Append(' ');
                                }
                                sbArgs.Append(args[cur]);
                            }

                            // 문자열을 완성시켜 전달해줍니다.
                            strArgsToHandover = sbArgs.ToString();

                            targetProgram.Arguments = strArgsToHandover;
                            sbLog.AppendLine(STR_INDENT + "with parameters: " + strArgsToHandover);
                        }

                        try
                        {
                            // 프로세스를 가동 준비합니다.
                            System.Diagnostics.Process procTargetProgram = new System.Diagnostics.Process{ StartInfo = targetProgram };

                            // Exit 이벤트를 걸어서 현재 스레드를 기다리지 않게 합니다. 그러나 여기에선 기다려야합니다. 주석처리
                            //https://msdn.microsoft.com/en-us/library/system.diagnostics.process.exited.aspx
                            //procTargetProgram.EnableRaisingEvents = true;
                            //procTargetProgram.Exited += new EventHandler(myProcess_Exited);

                            // 프로세스를 시작합니다.
                            procTargetProgram.Start();
                            sbLog.AppendLine("Launched " + strTargetProgramName + ' ' + strArgsToHandover);

                            //기다려야한다면 기다립니다.
                            if (haveToWaitForFinish)
                            {
                                procTargetProgram.WaitForExit();
                                sbLog.AppendLine("Finished. Exit code was " +procTargetProgram.ExitCode);
                            }
                        }
                        catch (Exception e)
                        {
                            hasError = true;
                            sbLog.AppendLine("ERROR launching " + strTargetProgramName + ' ' + strArgsToHandover);
                            sbLog.AppendLine(STR_INDENT + e.Message);
                        }
                    }
                    else
                    {
                        sbLog.AppendLine("Preparing to launch in minized mode: " + strTargetProgramName);

                        if (args.Length >= 2 + nCmdStart)
                        {
                            // 매개변수의 갯수가 2개 이상인경우 다음 매개변수를 모두 합칩니다.
                            StringBuilder sbArgs = new StringBuilder();
                            for (int cur = 1 + nCmdStart; cur < args.Length; cur++)
                            {
                                //첫번째가 아닌 경우 사이에 빈 칸을 넣어줍니다.
                                if (cur != 1 + nCmdStart)
                                {
                                    sbArgs.Append(' ');
                                }
                                sbArgs.Append(args[cur]);
                            }

                            // 문자열을 완성시켜 전달해줍니다.
                            strArgsToHandover = sbArgs.ToString();
                            sbLog.AppendLine(STR_INDENT + "with parameters: " + strArgsToHandover);
                        }

                        // 프로그램이 존재하는지 확인합니다.
                        if (! System.IO.File.Exists(strTargetProgramName))
                        {
                            hasError = true;
                            sbLog.AppendLine("ERROR, Program not found.");
                            sbLog.AppendLine(STR_INDENT + "Check program's path and try again.");
                            sbLog.AppendLine(STR_INDENT + "Surrounding program's path with double-quote marks may fix this error.");
                        }
                        else
                        {
                            // 프로그램이 존재하므로 시작시킵니다.
                            // unManaged 코드에서는 예외를 처리할 수 없기 때문에 경우의 수를 직접 찾아야합니다.
                            ProcessLaunchInfo pliLaunch = PreventFocusUtil.startProcessNoActivate(strTargetProgramName, strArgsToHandover, haveToWaitForFinish);
                            if (pliLaunch.bLaunched)
                            {
                                if (haveToWaitForFinish)
                                {
                                    sbLog.AppendLine("Finished. Exit code was " + pliLaunch.exitCode);
                                }
                                else
                                {
                                    sbLog.AppendLine("Launched.");
                                }
                            }
                            else
                            {
                                hasError = true;
                                sbLog.AppendLine("ERROR launching " + strTargetProgramName + ' ' + strArgsToHandover);
                                sbLog.AppendLine(STR_INDENT + "Exit code was " + pliLaunch.exitCode);
                                sbLog.AppendLine(STR_INDENT + "Check required privileges and other things");
                                sbLog.AppendLine(STR_INDENT + "that may prevent program from launching.");
                            }
                        }
                    }
                }
            }

            //모든 명령이 끝났습니다.

            if (hasError)
            {
                //오류가 있었습니다. 메시지 상자를 표시합니다.
                System.Windows.Forms.MessageBox.Show(sbLog.ToString());
            }
            else
            {
                //오류가 없었다면 그냥 종료합니다.
            }
        }

        static String getHelp(String strErrorMsg)
        {
            StringBuilder sbHelp = new StringBuilder();

            sbHelp.AppendLine("ERROR in command: " + strErrorMsg);
            sbHelp.AppendLine(); //빈 줄 삽입

            sbHelp.AppendLine(Assembly.GetExecutingAssembly().GetName().Name + " (" + Assembly.GetExecutingAssembly().GetName().Version + (IS_DEBUG ? " Debug Binary" : "") + ")");
            sbHelp.AppendLine();

            sbHelp.AppendLine("DESCRIPTION:");
            sbHelp.AppendLine(STR_INDENT + "This program launches a program with arguments, in minimized way.");
            sbHelp.AppendLine();

            //현재 실행중인 자신의 이름을 사용합니다.
            String strProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            sbHelp.AppendLine("USAGE:");
            sbHelp.AppendLine(STR_INDENT + strProcessName + " ["+ STR_CMD_LAUNCH + " | "+ STR_CMD_WAIT + "]" + " program arg1 arg2...");
            sbHelp.AppendLine();
            sbHelp.AppendLine(STR_INDENT + STR_CMD_LAUNCH);
            sbHelp.AppendLine(STR_INDENT + STR_INDENT + "launch program and quit quickly.");
            if (STR_CMD_LAUNCH.Equals(STR_CMD_DEFAULT))
            {
                sbHelp.AppendLine(STR_INDENT + STR_INDENT + "This is default behaviour.");
            }
            sbHelp.AppendLine(STR_INDENT + STR_CMD_WAIT);
            sbHelp.AppendLine(STR_INDENT + STR_INDENT + "launch program and wait for it finishes.");
            if (STR_CMD_WAIT.Equals(STR_CMD_DEFAULT))
            {
                sbHelp.AppendLine(STR_INDENT + STR_INDENT + "This is default behaviour.");
            }
            sbHelp.AppendLine();

            return sbHelp.ToString();
        }
    }
}
