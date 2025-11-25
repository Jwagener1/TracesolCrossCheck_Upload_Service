namespace TracesolCrossCheck_Upload_Service.Models;

public sealed class DailyStatsRecord
{
    public DateTime StatDate { get; set; }
    public int TotalScans { get; set; }
    public int SKU_Count { get; set; }
    public int Pallet_Count { get; set; }
    public int OCR_Description_1_Count { get; set; }
    public int Quantity_Count { get; set; }
    public int Batch_Number_Count { get; set; }
    public int Barcode_Count { get; set; }
    public int OCR_Description_2_Count { get; set; }
    public int Cross_Check_Count { get; set; }
    public int Label_Printed_Count { get; set; }
    public int Label_Applied_Count { get; set; }
    public int Check_Scan_Result_Count { get; set; }
    public int Valid_Count { get; set; }
    public int Sent_Count { get; set; }
    public int ImageSent_Count { get; set; }
    public int Duplicate_Count { get; set; }
    public int Complete_Count { get; set; }
    public int IC1_Good_Read_Count { get; set; }
    public int IC1_No_Read_Count { get; set; }
    public int IC2_Good_Read_Count { get; set; }
    public int IC2_No_Read_Count { get; set; }
    public int Cross_Check_Fail_Count { get; set; }
    public int CheckScan_Good_Read_Count { get; set; }
    public int CheckScan_No_Read_Count { get; set; }
}
