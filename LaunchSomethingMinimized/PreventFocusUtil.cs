using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace LaunchSomethingMinimized
{
    class PreventFocusUtil
    {
        // http://stackoverflow.com/a/19049930
        // Set si.wShowWindow to SW_SHOWNOACTIVATE to show the window normally
        // but without stealing focus, and SW_SHOWMINNOACTIVE to start the app minimised,
        // again without stealing focus.
        // A full list of options is available here: http://msdn.microsoft.com/en-us/library/windows/desktop/ms633548(v=vs.85).aspx


        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll")]
        static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        const int STARTF_USESHOWWINDOW = 1;
        const int SW_SHOWNOACTIVATE = 4;
        const int SW_SHOWMINNOACTIVE = 7;


        public static ProcessLaunchInfo startProcessNoActivate(string program, string arguments, bool haveToWaitFinishes)
        {
            // 프로그램의 작업 디렉토리를 확인합니다.
            string workDir = System.IO.Path.GetDirectoryName(program);
            if (workDir.Length <= 0)
            {
                // 작업 디렉토리 문자열의 길이가 0인 경우
                // 호출 디렉토리와 같은 곳에 있다는 뜻이므로
                // 실행 불능 오류 발생하는 빈 칸 대신 null을 넣습니다.
                workDir = null;
            }

            // 실행 정보를 보관했다가 실행 이후 활용하려합니다.
            ProcessLaunchInfo pliProcess = new ProcessLaunchInfo();

            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.dwFlags = STARTF_USESHOWWINDOW;
            // 이 부분이 중요합니다. 최소화시킨 상태에서 포커스도 갖지 않도록 호출하게 합니다.
            si.wShowWindow = SW_SHOWMINNOACTIVE; 

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            // 새 프로세스와 그의 주 스레드를 생성합니다.
            // 호출하는 프로세스의 보안 컨텍스트를 상속받습니다.
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms682425(v=vs.85).aspx
            pliProcess.bLaunched = CreateProcess(
                program,        // 실행시킬 프로그램 이름
                arguments,      // 실행시킬 명령행 (최대 MAX_PATH:260 자)
                IntPtr.Zero,    // 보안 속성(SECURITY_ATTRIBUTES) 구조를 가리키는 포인터. 프로세스용
                IntPtr.Zero,    // 보안 속성(SECURITY_ATTRIBUTES) 구조를 가리키는 포인터. 스레드용
                true,           // 호출하는 핸들의 상속 여부
                0,              // 프로세스 생성 플래그. 여기에서 프로세스 우선순위도 지정 가능
                IntPtr.Zero,    // 실행 환경을 나타내는 포인터. 유니코드/ANSI 등의 인코딩의 영향을 줌. (NULL인 경우 호출 프로세스의 실행환경 상속)
                workDir,        // 작업 디렉토리. (NULL인 경우 호출 프로세스의 위치를 작업 디렉토리로 사용)
                ref si,         // 실행 방법에 대한 정보 
                out pi          // 프로세스에 대한 정보
                );

            // 종료를 기다려야하는 경우 기다려줍니다.
            if (haveToWaitFinishes)
            {
                ProcessWaitHandle waitable = new ProcessWaitHandle(pi.hProcess);
                waitable.WaitOne();
            }

            // pInvoke를 통해 프로세스의 exitCode를 가져옵니다.
            //http://www.pinvoke.net/default.aspx/kernel32.getexitcodeprocess
            uint exitCode = 0;
            GetExitCodeProcess(pi.hProcess, out exitCode);

            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);

            // ExitCode 속성은 부호 있는 32비트 정수입니다.
            // 이 속성이 음수 값을 반환하지 않게 하려면
            // 0x80000000 보다 크거나 같은 수를 쓰지 마십시오.
            //https://msdn.microsoft.com/en-us/library/system.environment.exitcode(v=vs.110).aspx
            if (exitCode >= 0x80000000)
            {
                // 2의 보수로 이루어진 음수 표현 체계에 맞게 보정해줍니다.
                // 최상위 비트를 1로 만든 다음,
                // unsigned int인 exitCode에서 0x7FFFFFFF로 걸러낸 값을 int에 맞게 우겨넣습니다.
                pliProcess.exitCode = 1 << (32 - 1) | (int)(exitCode & 0x7FFFFFFF);
            }
            else
            {
                pliProcess.exitCode = (int)exitCode;
            }

            return pliProcess;
        }
    }

    // http://stackoverflow.com/a/2980461
    // The PROCESS_INFORMATION[ http://msdn.microsoft.com/en-us/library/ms684873(v=VS.85).aspx ] returns the handle to the newly created process (hProcess),
    // you can wait on this handle which will become signaled when the process exits.
    // You can use SafeWaitHandle[ http://msdn.microsoft.com/en-us/library/microsoft.win32.safehandles.safewaithandle.aspx ] encapsulate the handle and then use WaitHandle.WaitOne[http://msdn.microsoft.com/en-us/library/58195swd(v=VS.100).aspx] to wait for the process to exit.
    class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(IntPtr processHandle)
        {
            this.SafeWaitHandle = new SafeWaitHandle(processHandle, false);
        }
    }
    
    class ProcessLaunchInfo
    {
        public bool bLaunched;
        //public string strMessageIfFailed;
        public int exitCode;
    }
}
