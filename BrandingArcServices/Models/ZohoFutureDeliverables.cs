using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrandingArcServices.Models
{
    [Table("zohofuturedeliverables")]
    public class ZohoFutureDeliverables
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string? Company_Name { get; set; }
        public string? Company_Id { get; set; }
        public string? Deliverables_Id { get; set; }
        public string? Name { get; set; }
        public string? Topic_Category { get; set; }
        public string? Main_Status { get; set; }
        public DateTime? Due_Date { get; set; }
        public string? Main_Status1 { get; set; }
        public DateTime? Created_Date { get; set; }
        public DateTime? From_Date { get; set; }
        public DateTime? To_Date { get; set; }
    }
}