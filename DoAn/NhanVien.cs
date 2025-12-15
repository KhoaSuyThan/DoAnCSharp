namespace DoAn
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("NhanVien")]
    public partial class NhanVien
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int STT { get; set; }

        [Required]
        [StringLength(20)]
        public string MaNhanVien { get; set; }

        [StringLength(100)]
        public string TenNhanVien { get; set; }

        [StringLength(10)]
        public string GioiTinh { get; set; }

        [StringLength(15)]
        public string SDT { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(50)]
        public string ChucVu { get; set; }

        [StringLength(50)]
        public string CaLamViec { get; set; }

        [Column(TypeName = "date")]
        public DateTime? NgaySinh { get; set; }

        [StringLength(200)]
        public string DiaChi { get; set; }

        [StringLength(50)]
        public string TaiKhoan { get; set; }

        [StringLength(255)]
        public string MatKhau { get; set; }
    }
}
