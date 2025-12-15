using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;

namespace DoAn
{
    public partial class Model1 : DbContext
    {
        public Model1()
            : base("name=Model1")
        {
        }

        public virtual DbSet<KhachHang> KhachHangs { get; set; }
        public virtual DbSet<NhanVien> NhanViens { get; set; }
        public virtual DbSet<Phong> Phongs { get; set; }
        public virtual DbSet<TongDoanhThu> TongDoanhThus { get; set; }
        public virtual DbSet<DichVu> DichVus { get; set; }
        public virtual DbSet<ChiTietSuDungDV> ChiTietSuDungDVs { get; set; }

        [Table("TongDoanhThu")]
        public class TongDoanhThu
        {
            [Key]
            public int ID { get; set; }

            public decimal TongTien { get; set; }

            public DateTime NgayCapNhat { get; set; }
        }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // C?u hình ?? chính xác cho GiaThue (decimal(18,0))
            modelBuilder.Entity<KhachHang>()
                .Property(k => k.GiaThue)
                .HasPrecision(18, 0);

            // Cấu hình cho SoLuong trong ChiTietSuDungDV (decimal(10,2))
            modelBuilder.Entity<ChiTietSuDungDV>()
                .Property(ct => ct.SoLuong)
                .HasPrecision(10, 2);

            // Cấu hình cho DonGia trong DichVu (decimal(18,0))
            modelBuilder.Entity<DichVu>()
                .Property(dv => dv.DonGia)
                .HasPrecision(18, 0);
        }
    }
}
