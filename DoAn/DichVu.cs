namespace DoAn
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DichVu")] // Tên bảng trong CSDL
    public partial class DichVu
    {
        // Constructor để khởi tạo danh sách
        public DichVu()
        {
            ChiTietSuDungDVs = new HashSet<ChiTietSuDungDV>();
        }

        [Key] // Khóa chính
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Tự động tăng
        public int MaDV { get; set; }

        [Required] // NOT NULL
        [StringLength(100)]
        public string TenDV { get; set; }

        [Required]
        [StringLength(20)]
        public string DonViTinh { get; set; }

        public decimal DonGia { get; set; }

        [Required]
        [StringLength(50)]
        public string LoaiDichVu { get; set; }

        public bool? Active { get; set; } // Dùng bool? (nullable) cho phép cột DEFAULT

        // Quan hệ: Một Dịch Vụ có thể được dùng trong nhiều Chi Tiết
        public virtual ICollection<ChiTietSuDungDV> ChiTietSuDungDVs { get; set; }
    }
}