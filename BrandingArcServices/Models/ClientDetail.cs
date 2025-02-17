using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrandingArcServices.Models
{
    [Table("ClientDetails")]
    public class ClientDetail
	{
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string? Name { get; set; }

        public string? PublicURL { get; set; }

        public int? PropertyId { get; set; }

        public string ClientUID { get; set; }

        public string SearchConsoleId { get; set; }

        public string CFPBName { get; set; }

        public DateTime? WebsiteLaunchDate { get; set; }

        public string ZohoAccountId { get; set; }

        public string LinkedinId { get; set; }
    }
}

