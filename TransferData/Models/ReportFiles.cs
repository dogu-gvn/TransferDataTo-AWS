using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferData.Models
{
    public class ReportFiles
    {
        public int ReportFileID { get; set; }
        public int ReportID { get; set; }
        public string ReportFilePath { get; set; }
        public string URL1 { get; set; }
        public string URL2 { get; set; }
        public bool isFromAzure { get; set; }
    }
}
