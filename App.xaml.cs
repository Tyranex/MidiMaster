using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;

namespace SS14_MIDI_IDE;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ev) => 
        {
            File.WriteAllText("crash.log", "Unhandled: " + ev.ExceptionObject.ToString());
        };
        
        DispatcherUnhandledException += (s, ev) => 
        {
            File.WriteAllText("crash.log", "Dispatcher: " + ev.Exception.ToString());
            ev.Handled = true;
        };
        
        TaskScheduler.UnobservedTaskException += (s, ev) => 
        {
            File.WriteAllText("crash.log", "Task: " + ev.Exception.ToString());
        };

        base.OnStartup(e);
    }
}
