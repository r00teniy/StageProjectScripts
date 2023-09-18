using Autodesk.AutoCAD.Runtime;

using StageProjectScripts.Forms;
using StageProjectScripts.Functions;

[assembly: CommandClass(typeof(StageProjectScripts.MyCommands))]

namespace StageProjectScripts
{
    internal class MyCommands
    {
        [CommandMethod("ProjectScripts")]
        static public void StageProjectScripts()
        {
            var settingsStorage = new SettingsStorage();
            var variables = settingsStorage.ReadSettingsFromXML();
            variables.SavedData = settingsStorage.ReadData();
            var MW = new MainWindow(variables);
            MW.Show();
        }
        [CommandMethod("ProjectChecks")]
        static public void StageProjectChecks()
        {
            var settingsStorage = new SettingsStorage();
            var variables = settingsStorage.ReadSettingsFromXML();
            variables.SavedData = settingsStorage.ReadData();
            var CW = new ChecksWindow(variables);
            CW.Show();
        }
    }
}