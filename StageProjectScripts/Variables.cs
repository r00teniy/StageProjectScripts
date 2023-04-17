using System.Collections.Generic;

namespace StageProjectScripts
{
    public class Variables
    {
        public string[] SavedData { get; set; }// = new string[3];
        //Layers for plot borders
        public string PlotLayer { get; set; }// = "11_Граница_ЗУ_КН_";
        //Layers for Hatches
        public string[] LaylistHatch { get; set; }// = { "41_Покр_Проезд", "49_Покр_Щебеночный_проезд", "42_Покр_Тротуар", "42_Покр_Тротуар_пожарный", "43_Покр_Отмостка", "44_Покр_Детская_площадка", "45_Покр_Спортивная_площадка", "46_Покр_Площадка_отдыха", "47_Покр_Хоз_площадка", "48_Покр_Площадка_для_собак", "51_Газон", "52_Газон_рулонный", "53_Газон_пожарный" };
        //Layers for polylines to extract length from
        public string[] LaylistPlL { get; set; }// = { "31_Борт_БР100.30.15", "32_Борт_БР100.20.8", "33_Борт_БР100.45.18", "33_Борт_БР100.60.20", "34_Борт_Металл", "35_Борт_Пластик", "36_Борт_1ГП_100.30.15", "37_Борт_4ГП_100.20.10", "38_Борт_2ГП_100.40.18", "38_Борт_3ГП_100.60.20" };
        //Layers for polylines to extract area from
        public string[] LaylistPlA { get; set; }// = { "09_Граница_благоустройства", "16_Здание_контур_площадь_застройки" };
        //Layers for blocks
        public string[] LaylistBlockCount { get; set; }// = { "51_Деревья", "52_Кустарники" };
        public string GreeneryMleaderStyleName { get; set; }// = "Озеленение";
        public string GreeneryMleaderBlockName { get; set; }// = "Выноска_озеленение";
        public string[] GreeneryId { get; set; }// = { "1", "2" };
        public string[] GreeneryAttr { get; set; }// = { "НОМЕР", "КОЛ-ВО" };
        public double[] GreeneryGroupingDistance { get; set; }// = { 6.1, 4.1 };
        public string[] LaylistBlockWithParams { get; set; }// = { "12_Тактильная_плитка" };
        //Block details names
        public List<List<string>> BlockDetailsParameters { get; set; }// = new() { new List<string> { "Тип", "0", "Длина", "1" } };
        public List<List<string>> BlockDetailsParametersVariants { get; set; }// = new() { new List<string> { "Линии вдоль", "Линии поперек", "Конусы шахматный", "Конусы квадрат", "1 Линия", "2 Линии", "Шуцлиния" } };
        //Layer for pavement labels
        public string PLabelLayer { get; set; }// = "32_Подписи_покрытий";
        public string[] PLabelValues { get; set; }// = { "1", "1А", "2", "2А", "3", "4", "4", "2", "2", "5" };
        //Layer for greenery labels
        public string OLabelLayer { get; set; }// = "50_Озеленение_подписи";
        //Temporary layer data
        public string TempLayer { get; set; }// = "80_Временная_геометрия"; // layer for temporary geometry
        public short TempLayerColor { get; set; }// = Color.FromColorIndex(ColorMethod.ByAci, 3);
        public double TempLayerLineWeight { get; set; }// = 2.0;
        public bool TempLayerPrintable { get; set; }// = false;
        //Table handles
        public string Th { get; set; }// = "774A"; //Table handle for hatches
        public string Tpl { get; set; }// = "78E16"; //Table handle for polylines for length
        public string Tpa { get; set; }// = "88C28"; //Table handle for polylines for area
        public string Tbn { get; set; }// = "78E73"; //Table handle for normal blocks
        public string Tbp { get; set; }// = "A2E97"; //Table handle for blocks with params
        //Temporary
        public int[] CurbLineCount { get; set; }// = { 2, 1, 2, 2, 1, 1, 2, 1, 2, 2 };

        public Variables()
        {
        }
    }
}
