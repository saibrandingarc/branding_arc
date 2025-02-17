using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrandingArcServices.Models
{
    [Table("zohocasespermonth")]
    public class ZohoCasesMonth
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public long? Case_Number { get; set; }

        public string? Account_Name { get; set; }

        public string? Account_Id { get; set; }

        public string? Description { get; set; }

        public string? Case_Id { get; set; }

        public DateTime? Case_Closed_Date { get; set; }

        public DateTime? Created_Date { get; set; }

        public DateTime? From_Date { get; set; }

        public DateTime? To_Date { get; set; }
    }
}

