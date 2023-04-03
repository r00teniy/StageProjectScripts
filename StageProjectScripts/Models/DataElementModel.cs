namespace StageProjectScripts.Models;

internal class DataElementModel
{
    internal double Amount { get; set; }
    internal int NumberInTable { get; set; }
    internal bool IsInsidePlot { get; set; }

    public DataElementModel(double amount, int numberInTable, bool isInsidePlot)
    {
        Amount = amount;
        NumberInTable = numberInTable;
        IsInsidePlot = isInsidePlot;
    }
}
