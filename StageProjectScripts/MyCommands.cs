using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[assembly: CommandClass(typeof(StageProjectScripts.MyCommands))]

namespace StageProjectScripts
{
    internal class MyCommands
    {
        [CommandMethod("Command")]
        static public void Command()
        {

        }
    }
}