using Autodesk.AutoCAD.Runtime;

using StageProjectScripts.Forms;

[assembly: CommandClass(typeof(StageProjectScripts.MyCommands))]

namespace StageProjectScripts
{
    internal class MyCommands
    {
        [CommandMethod("StageProjectScripts")]
        static public void StageProjectScripts()
        {
            Variables.savedData = Functions.SettingsStorage.ReadData();
            var MW = new MainWindow();
            MW.Show();
        }
        [CommandMethod("StageProjectChecks")]
        static public void StageProjectChecks()
        {
            Variables.savedData = Functions.SettingsStorage.ReadData();
            var CW = new ChecksWindow();
            CW.Show();
        }
    }
}