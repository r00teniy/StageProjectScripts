using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using StageProjectScripts;

namespace ProjectScripts.Functions;
internal class LayerSetup
{
    internal void SetViewPortLayersVisibility(Variables variables, ObjectId viewPortId, int settingsNumber, string xRef, string plotXref, string plotNumber)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var db = doc.Database;

        var showInViewPortNormal = variables.LayersToShowInViewPortNormal[settingsNumber].Split(',');
        var hideInViewPortNormal = variables.LayersToHideInViewPortNormal[settingsNumber].Split(',');
        var showInViewPortOsnova = variables.LayersToShowInViewPortOsnova[settingsNumber].Split(',');
        var hideInViewPortOsnova = variables.LayersToHideInViewPortOsnova[settingsNumber].Split(',');
        var showInViewPortBorders = new string[0];
        var hideInViewPortBorders = new string[0];

        if (!variables.JustPlotBorders[settingsNumber])
        {
            showInViewPortBorders = variables.LayersToShowInViewPortBorders[settingsNumber].Split(',');
            hideInViewPortBorders = variables.LayersToHideInViewPortBorders[settingsNumber].Split(',');
        }

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock docLock = doc.LockDocument())
            {
                var lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                var vp = tr.GetObject(viewPortId, OpenMode.ForWrite) as Viewport;
                var layerIdsToShow = new ObjectIdCollection();
                var layerIdsToHide = new ObjectIdCollection();

                foreach (var item in lt)
                {
                    var ltr = tr.GetObject(item, OpenMode.ForRead) as LayerTableRecord;
                    for (int i = 0; i < showInViewPortNormal.Length; i++)
                    {
                        if (ltr.Name.Contains(showInViewPortNormal[i]))
                        {
                            layerIdsToShow.Add(item);
                        }
                    }
                    for (int i = 0; i < hideInViewPortNormal.Length; i++)
                    {
                        if (ltr.Name.Contains(hideInViewPortNormal[i]))
                        {
                            layerIdsToHide.Add(item);
                        }
                    }
                    for (int i = 0; i < showInViewPortOsnova.Length; i++)
                    {
                        if (ltr.Name.Contains(xRef + "|" + showInViewPortOsnova[i]))
                        {
                            layerIdsToShow.Add(item);
                        }
                    }
                    for (int i = 0; i < hideInViewPortOsnova.Length; i++)
                    {
                        if (ltr.Name.Contains(xRef + "|" + hideInViewPortOsnova[i]))
                        {
                            layerIdsToHide.Add(item);
                        }
                    }
                    if (!variables.JustPlotBorders[settingsNumber])
                    {
                        for (int i = 0; i < showInViewPortBorders.Length; i++)
                        {
                            if (ltr.Name.Contains(plotXref + "|" + showInViewPortBorders[i]))
                            {
                                layerIdsToShow.Add(item);
                            }
                        }
                        for (int i = 0; i < hideInViewPortBorders.Length; i++)
                        {
                            if (ltr.Name.Contains(plotXref + "|" + hideInViewPortBorders[i]))
                            {
                                layerIdsToHide.Add(item);
                            }

                        }
                    }
                    else
                    {
                        if (ltr.Name.Contains(plotXref + "|"))
                        {
                            layerIdsToHide.Add(item);
                        }

                    }
                    if (ltr.Name.Contains(plotXref + "|" + variables.PlotLayer + plotNumber.Replace(':', '_')))
                    {
                        layerIdsToShow.Add(item);
                    }
                }
                vp.FreezeLayersInViewport(layerIdsToHide.GetEnumerator());
                vp.ThawLayersInViewport(layerIdsToShow.GetEnumerator());
                tr.Commit();
            }
        }

    }
}
