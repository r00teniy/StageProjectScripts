using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.Colors;

namespace StageProjectScripts
{
    internal static class Variables
    {
        internal static string[] savedData = new string[3];
        //Layers for plot borders
        internal static string plotLayers = "11_Граница_ЗУ_КН_";
        //Layers for Hatches
        internal static string[] laylistHatch = { "41_Покр_Проезд", "49_Покр_Щебеночный_проезд", "42_Покр_Тротуар", "42_Покр_Тротуар_пожарный", "43_Покр_Отмостка", "44_Покр_Детская_площадка", "45_Покр_Спортивная_площадка", "46_Покр_Площадка_отдыха", "47_Покр_Хоз_площадка", "48_Покр_Площадка_для_собак", "51_Газон", "52_Газон_рулонный", "53_Газон_пожарный" };
        //Layers for polylines to extract length from
        internal static string[] laylistPlL = { "31_Борт_БР100.30.15", "32_Борт_БР100.20.8", "33_Борт_БР100.45.18", "33_Борт_БР100.60.20", "34_Борт_Металл", "35_Борт_Пластик", "36_Борт_1ГП_100.30.15", "37_Борт_4ГП_100.20.10", "38_Борт_2ГП_100.40.18", "38_Борт_3ГП_100.60.20" };
        //Layers for polylines to extract area from
        internal static string[] laylistPlA = { "09_Граница_благоустройства", "16_Здание_контур_площадь_застройки" };
        //Layers for blocks
        internal static string[] laylistBlockCount = { "51_Деревья", "52_Кустарники" };
        internal static string[] laylistBlockWithParams = { "12_Тактильная_плитка" };
        //Block details names
        internal static List<string>[] blockDetailsParameters = { new List<string> { "Тип", "Длина" } };
        internal static List<string>[] blockDetailsParametersVariants = { new List<string> { "Линии вдоль", "Линии поперек", "Конусы шахматный", "Конусы квадрат", "1 Линия", "2 Линии", "Шуцлиния" } };
        //Layer for pavement labels
        internal static string pLabelLayer = "32_Подписи_покрытий";
        //Layer for greenery labels
        internal static string oLabelLayer = "50_Озеленение_подписи";
        //Temporary layer data
        internal static string tempLayer = "80_Временная_геометрия"; // layer for temporary geometry
        internal static Color tempLayerColor = Color.FromColorIndex(ColorMethod.ByAci, 3);
        internal static double tempLayerLineWeight = 2.0;
        internal static bool tempLayerPrintable = false;
        //Table handles
        internal static long th = Convert.ToInt64("774A", 16); //Table handle for hatches
        internal static long tpl = Convert.ToInt64("78E16", 16); //Table handle for polylines for length
        internal static long tpa = Convert.ToInt64("88C28", 16); //Table handle for polylines for area
        internal static long tbn = Convert.ToInt64("78E73", 16); //Table handle for normal blocks
        internal static long tbp = Convert.ToInt64("A2E97", 16); //Table handle for blocks with params
        //Temporary
        internal static int[] curbLineCount = { 2, 1, 2, 2, 1, 1, 2, 1, 2, 2 };
    }
}
