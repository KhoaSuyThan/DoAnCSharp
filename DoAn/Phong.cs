namespace DoAn
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Phong")]
    public partial class Phong
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int STT { get; set; }
        public string SoPhong { get; set; }
        [Required]

        public int? Tang { get; set; }

        [StringLength(50)]
        public string LoaiPhong { get; set; }

        public decimal? Gia { get; set; }

        [StringLength(50)]
        public string TinhTrang { get; set; }

        public int? SoGioThue { get; set; }
        public DateTime? NgayThuePhong { get; set; } // Phải là DateTime? (nullable)
        public DateTime? NgayTraPhong { get; set; }
    }
}
