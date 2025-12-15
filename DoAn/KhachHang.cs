using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAn
{
    [Table("KhachHang")]
    public partial class KhachHang
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int STT { get; set; }

        [Required]
        [StringLength(100)]
        public string TenKhachHang { get; set; }

        [StringLength(20)]
        public string CCCD { get; set; }

        [StringLength(50)]
        public string QuocTich { get; set; }

        [Required]
        [StringLength(10)]
        public string SoPhong { get; set; }

        [StringLength(50)]
        public string LoaiPhong { get; set; }

        [StringLength(10)]
        public string GioiTinh { get; set; }

        [Required]
        public DateTime NgayThue { get; set; }

        public DateTime? NgayTra { get; set; }

        [StringLength(15)]
        public string SDT { get; set; }

        // ✅ Sửa lỗi: EF6 chỉ hiểu "decimal", không hiểu "decimal(18,0)"
        [Column(TypeName = "decimal")]
        public decimal? GiaThue { get; set; }

        [StringLength(255)]
        public string DiaChi { get; set; }

        [Column(TypeName = "date")]
        public DateTime? NgaySinh { get; set; }
    }
}
