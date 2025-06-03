CREATE DATABASE QLNS;
GO
USE QLNS;
GO

--A.TẠO BẢNG

-- 1. Thể loại
CREATE TABLE TheLoai (
    MaTheLoai VARCHAR(10) PRIMARY KEY,
    TenTheLoai NVARCHAR(100) NOT NULL,
    MoTa NVARCHAR(255)
);

-- 2. Tác giả
CREATE TABLE TacGia (
    MaTacGia VARCHAR(10) PRIMARY KEY,
    HoTen NVARCHAR(100) NOT NULL,
    TieuSu NVARCHAR(500),
    NamSinh INT,
    QueQuan NVARCHAR(100)

);

-- 3. Nhà xuất bản
CREATE TABLE NhaXuatBan (
    MaNXB VARCHAR(10) PRIMARY KEY,
    TenNXB NVARCHAR(100) NOT NULL,
    DiaChi NVARCHAR(200),
    SDT NVARCHAR(20),
    Email NVARCHAR(100)
);

-- 4. Sách
CREATE TABLE Sach (
    MaSach VARCHAR(10) PRIMARY KEY,
    TieuDe NVARCHAR(200) NOT NULL,
    GiaBia DECIMAL(12,2) NOT NULL,
    MoTa NVARCHAR(255),
    LanTaiBan INT CHECK (LanTaiBan >= 0),
    NamXuatBan INT,
    MaNXB VARCHAR(10) FOREIGN KEY REFERENCES NhaXuatBan(MaNXB),
    TinhTrang NVARCHAR(50) CHECK (TinhTrang IN (N'Còn hàng', N'Hết hàng', N'Ngưng bán')) NOT NULL,
    SoLuongTon INT check(SoLuongTon >= 0),
    IsDeleted BIT DEFAULT 0
);



--5 Sach_TheLoai
CREATE TABLE Sach_TheLoai (
    MaSach VARCHAR(10),
    MaTheLoai VARCHAR(10),
    PRIMARY KEY (MaSach, MaTheLoai),
    FOREIGN KEY (MaSach) REFERENCES Sach(MaSach),
    FOREIGN KEY (MaTheLoai) REFERENCES TheLoai(MaTheLoai)
);

--6. Sach_TacGia
CREATE TABLE Sach_TacGia (
    MaSach VARCHAR(10),
    MaTacGia VARCHAR(10),
    PRIMARY KEY (MaSach, MaTacGia),
    FOREIGN KEY (MaSach) REFERENCES Sach(MaSach),
    FOREIGN KEY (MaTacGia) REFERENCES TacGia(MaTacGia)
);

-- 7. Khách hàng
CREATE TABLE KhachHang (
    MaKH VARCHAR(10) PRIMARY KEY,
    HoTen NVARCHAR(100),
    DiaChi NVARCHAR(200),
    SDT NVARCHAR(20),
    Email NVARCHAR(100),
    TongTien DECIMAL(18,2) NULL,
    Hang NVARCHAR(20) NULL
        CHECK (Hang IN (N'VIP', N'Thường', N'Mới')) --Thêm xếp hạng để dùng cusor
);

ALTER TABLE KhachHang 
ADD CONSTRAINT UQ_KhachHang_SDT UNIQUE (SDT);
-- 8. Nhân viên
CREATE TABLE NhanVien (
    MaNV VARCHAR(10) PRIMARY KEY,
    HoTen NVARCHAR(100) NOT NULL,
    ChucVu NVARCHAR(50) CHECK (ChucVu IN (N'Quản lý', N'Nhân viên')),
    NgayVaoLam DATE,
    SDT NVARCHAR(20),
    Email NVARCHAR(100),
    CCCD NVARCHAR(12),
    IsDeleted BIT DEFAULT 0
);
ALTER TABLE NhanVien
ADD CONSTRAINT UQ_NhanVien_CCCD UNIQUE (CCCD);


-- 9. Khuyến mãi
CREATE TABLE KhuyenMai (
    MaKhuyenMai VARCHAR(10) PRIMARY KEY,
    MoTa NVARCHAR(255),
    NgayBatDau DATE,
    NgayKetThuc DATE,
    PhanTramGiamGia INT CHECK (PhanTramGiamGia BETWEEN 0 AND 100),
    IsDeleted BIT DEFAULT 0
);

-- 10. Hóa đơn
CREATE TABLE HoaDon(
    MaHD INT PRIMARY KEY IDENTITY,
    NgayLap DATE DEFAULT GETDATE(),
    TongTien DECIMAL(10,2),
    TrangThai NVARCHAR(50),
    MaKH VARCHAR(10) FOREIGN KEY REFERENCES KhachHang(MaKH),
    MaNV VARCHAR(10) FOREIGN KEY REFERENCES NhanVien(MaNV),
    MaKhuyenMai varchar(10) FOREIGN KEY REFERENCES KHUYENMAI(MaKhuyenMai),
    TongGiam DECIMAL(10,2) DEFAULT 0,
    ThanhTien AS (TongTien - TongGiam) PERSISTED
);
ALTER TABLE HoaDon
ALTER COLUMN MaNV VARCHAR(10) NULL;


ALTER TABLE HoaDon
ADD CONSTRAINT FK_HoaDon_MaNV
FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV)
ON DELETE SET NULL;

-- 11. Chi tiết hóa đơn
CREATE TABLE ChiTietHoaDon (
    MaHD INT,
    MaSach VARCHAR(10),
    SoLuong INT,
    DonGia DECIMAL(12,2),
    PRIMARY KEY (MaHD, MaSach),
    FOREIGN KEY (MaHD) REFERENCES HoaDon(MaHD),
    FOREIGN KEY (MaSach) REFERENCES Sach(MaSach)
);

-- 12. Nhà cung cấp
CREATE TABLE NhaCungCap (
    MaNCC VARCHAR(10) PRIMARY KEY,
    TenNCC NVARCHAR(100),
    DiaChi NVARCHAR(200),
    SDT NVARCHAR(20),
    Email NVARCHAR(100)
);

-- 13. Phiếu nhập
CREATE TABLE PhieuNhap (
    MaPhieuNhap INT PRIMARY KEY IDENTITY,
    NgayNhap DATE,
    TongChiPhi DECIMAL(12,2),
    MaNV VARCHAR(10) FOREIGN KEY REFERENCES NhanVien(MaNV),
    MaNCC VARCHAR(10) FOREIGN KEY REFERENCES NhaCungCap(MaNCC)
);

ALTER TABLE PhieuNhap
ALTER COLUMN MaNV VARCHAR(10) NULL;

ALTER TABLE PhieuNhap
ADD CONSTRAINT FK_PhieuNhap_MaNV
FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV)
ON DELETE SET NULL;

-- 14. Chi tiết phiếu nhập
CREATE TABLE ChiTietPhieuNhap (
    MaPhieuNhap INT,
    MaSach VARCHAR(10),
    SoLuong INT,
    GiaNhap DECIMAL(12,2),
    PRIMARY KEY (MaPhieuNhap, MaSach),
    FOREIGN KEY (MaPhieuNhap) REFERENCES PhieuNhap(MaPhieuNhap),
    FOREIGN KEY (MaSach) REFERENCES Sach(MaSach)
);

--16.Table ghi lại lịch sử thêm, xoá, sửa Nhân Viên
-- CREATE TABLE LogNhanVien (
--     LogID INT IDENTITY PRIMARY KEY,
--     MaNV VARCHAR(10),
--     HanhDong NVARCHAR(20),         -- 'Thêm', 'Sửa', 'Xoá'
--     ThoiGian DATETIME DEFAULT GETDATE(),
--     GhiChu NVARCHAR(255)
-- );

CREATE TABLE LogNhanVien (
    MaNV1 VARCHAR(10),  -- Nhân viên thực hiện hành động
    MaNV2 VARCHAR(10),  -- Nhân viên bị ảnh hưởng
    HanhDong NVARCHAR(20),         -- 'Thêm', 'Sửa', 'Xoá'
    ThoiGian DATETIME DEFAULT GETDATE(),
    GhiChu NVARCHAR(255)
    PRIMARY KEY (MaNV1, MaNV2),
);

--B.TẠO RÀNG BUỘC
--1.Thêm ràng buộc giá bìa cho sách
ALTER TABLE Sach
ADD CONSTRAINT CK_Sach_GiaBia
CHECK (GiaBia >= 1000 AND GiaBia % 500 = 0);

--2.Ràng buộc ngày khuyến bắt đầu <= ngày kết thúc
ALTER TABLE KhuyenMai
ADD CONSTRAINT CK_KhuyenMai_NgayHopLe
CHECK (NgayBatDau <= NgayKetThuc);

--C.TẠO STORED PROCEDURE

--1.Stored procedure thêm thể loại mới
GO
CREATE PROCEDURE sp_ThemTheLoai
    @MaTheLoai VARCHAR(10),
    @TenTheLoai NVARCHAR(255),
    @MoTa NVARCHAR(1000)
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM TheLoai WHERE MaTheLoai = @MaTheLoai)
    BEGIN
        INSERT INTO TheLoai (MaTheLoai, TenTheLoai, MoTa)
        VALUES (@MaTheLoai, @TenTheLoai, @MoTa)
    END
END

--2.Stored procedure thêm tác giả mới
GO
CREATE PROCEDURE sp_ThemTacGia
    @MaTacGia VARCHAR(10),
    @HoTen NVARCHAR(255),
    @TieuSu NVARCHAR(1000),
    @NamSinh INT,
    @QueQuan NVARCHAR(255)
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM TacGia WHERE MaTacGia = @MaTacGia)
    BEGIN
        INSERT INTO TacGia (MaTacGia, HoTen, TieuSu, NamSinh, QueQuan)
        VALUES (@MaTacGia, @HoTen, @TieuSu, @NamSinh, @QueQuan)
    END
END

--3.Stored procedure thêm nhà xuất bản mới
GO
CREATE PROCEDURE sp_ThemNXB
    @MaNXB VARCHAR(10),
    @TenNXB NVARCHAR(255),
    @DiaChi NVARCHAR(255),
    @SDT NVARCHAR(20),
    @Email NVARCHAR(255)
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM NhaXuatBan WHERE MaNXB = @MaNXB)
    BEGIN
        INSERT INTO NhaXuatBan (MaNXB, TenNXB, DiaChi, SDT, Email)
        VALUES (@MaNXB, @TenNXB, @DiaChi, @SDT, @Email)
    END
END
GO

--4.Stored procedure thêm sách
GO
CREATE PROCEDURE sp_ThemSach
    @MaSach VARCHAR(10),
    @TieuDe NVARCHAR(255),
    @GiaBia DECIMAL(10,0),
    @MoTa NVARCHAR(1000),
    @LanTaiBan INT,
    @NamXuatBan INT,
    @MaNXB VARCHAR(10),
    @TinhTrang NVARCHAR(50)
AS
BEGIN
    INSERT INTO Sach (MaSach, TieuDe, GiaBia, MoTa, LanTaiBan, NamXuatBan, MaNXB, TinhTrang)
    VALUES (@MaSach, @TieuDe, @GiaBia, @MoTa, @LanTaiBan, @NamXuatBan, @MaNXB, @TinhTrang)
END


--5.Stored procedure thêm Sach_TheLoai
GO
CREATE PROCEDURE sp_ThemSach_TheLoai
    @MaSach VARCHAR(10),
    @MaTheLoai VARCHAR(10)
AS
BEGIN
    INSERT INTO Sach_TheLoai (MaSach, MaTheLoai)
    VALUES (@MaSach, @MaTheLoai)
END

--6.Sotred procedure thêm Sach_TacGia
GO
CREATE PROCEDURE sp_ThemSach_TacGia
    @MaSach VARCHAR(10),
    @MaTacGia VARCHAR(10)
AS
BEGIN
    INSERT INTO Sach_TacGia (MaSach, MaTacGia)
    VALUES (@MaSach, @MaTacGia)
END

--7.Stored procedure sửa sách
GO
CREATE PROCEDURE sp_SuaSach
    @MaSach VARCHAR(10),
    @TieuDe NVARCHAR(255),
    @GiaBia DECIMAL(10, 0),
    @MoTa NVARCHAR(500),
    @LanTaiBan INT,
    @NamXuatBan INT,
    @MaNXB VARCHAR(10),
    @TinhTrang NVARCHAR(50)
AS
BEGIN
    UPDATE Sach
    SET
        TieuDe = @TieuDe,
        GiaBia = @GiaBia,
        MoTa = @MoTa,
        LanTaiBan = @LanTaiBan,
        NamXuatBan = @NamXuatBan,
        MaNXB = @MaNXB,
        TinhTrang = @TinhTrang
    WHERE MaSach = @MaSach
END

--8.Stored procedure xoá sách--   
go
CREATE OR ALTER  PROCEDURE sp_XoaSach
    @MaSach VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Sach WHERE MaSach = @MaSach AND isDeleted = 0)
    BEGIN
        UPDATE Sach SET isDeleted = 1 WHERE MaSach = @MaSach;
    END
END

--9. Stored procedure Tìm sách
GO
CREATE PROCEDURE sp_TimKiemSach_TuKhoa
    @TuKhoa NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @sql NVARCHAR(MAX)
    DECLARE @params NVARCHAR(MAX) = N'@TuKhoa NVARCHAR(100)'

    SET @sql = N'
    SELECT DISTINCT 
        s.MaSach, s.TieuDe, s.GiaBia, s.MoTa, s.LanTaiBan, s.NamXuatBan,
        s.MaNXB, s.TinhTrang, s.SoLuongTon,
        ISNULL(tgList.TacGiaList, N''Chưa có'') AS TacGia,
        ISNULL(tlList.TheLoaiList, N''Chưa có'') AS TheLoai
    FROM Sach s
    LEFT JOIN Sach_TacGia stg ON s.MaSach = stg.MaSach
    LEFT JOIN TacGia tg ON tg.MaTacGia = stg.MaTacGia
    LEFT JOIN Sach_TheLoai stl ON s.MaSach = stl.MaSach
    LEFT JOIN TheLoai tl ON tl.MaTheLoai = stl.MaTheLoai
    LEFT JOIN (
        SELECT stg.MaSach, STRING_AGG(tg.HoTen, '', '') AS TacGiaList
        FROM Sach_TacGia stg
        JOIN TacGia tg ON stg.MaTacGia = tg.MaTacGia
        GROUP BY stg.MaSach
    ) tgList ON tgList.MaSach = s.MaSach
    LEFT JOIN (
        SELECT stl.MaSach, STRING_AGG(tl.TenTheLoai, '', '') AS TheLoaiList
        FROM Sach_TheLoai stl
        JOIN TheLoai tl ON stl.MaTheLoai = tl.MaTheLoai
        GROUP BY stl.MaSach
    ) tlList ON tlList.MaSach = s.MaSach
    WHERE 
          s.MaSach COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR s.TieuDe COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR s.MoTa COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR CAST(s.LanTaiBan AS NVARCHAR) COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR CAST(s.NamXuatBan AS NVARCHAR) COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR s.MaNXB COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR CAST(s.GiaBia AS NVARCHAR) COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR s.TinhTrang COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR CAST(s.SoLuongTon AS NVARCHAR) COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR tg.HoTen COLLATE Latin1_General_CI_AI LIKE @TuKhoa
       OR tl.TenTheLoai COLLATE Latin1_General_CI_AI LIKE @TuKhoa
    '

    DECLARE @TuKhoaFilter NVARCHAR(100) = N'%' + @TuKhoa + '%'

    EXEC sp_executesql @sql, @params, @TuKhoa = @TuKhoaFilter
END



--10.Stored procedure Nhập sách
GO
CREATE PROCEDURE sp_NhapSach
    @MaPhieuNhap VARCHAR(10),
    @MaSach VARCHAR(10),
    @SoLuong INT,
    @GiaNhap DECIMAL(18,2)
AS
BEGIN
    -- Thêm chi tiết phiếu nhập
    INSERT INTO ChiTietPhieuNhap(MaPhieuNhap, MaSach, SoLuong, GiaNhap)
    VALUES (@MaPhieuNhap, @MaSach, @SoLuong, @GiaNhap);

    -- Cập nhật tồn kho sách
    UPDATE Sach
    SET SoLuongTon = SoLuongTon + @SoLuong
    WHERE MaSach = @MaSach;

    -- Cập nhật tổng tiền phiếu nhập
    UPDATE PhieuNhap
    SET TongChiPhi = TongChiPhi + (@GiaNhap * @SoLuong)
    WHERE MaPhieuNhap = @MaPhieuNhap;
END

---12. Lịch sử nhập sách
GO 
create PROCEDURE sp_LichSuNhapSach
AS
BEGIN
    SELECT 
        ctpn.MaPhieuNhap AS ID,
        ctpn.MaSach AS MaSach,
        ctpn.SoLuong AS SoLuong,
        ctpn.GiaNhap AS GiaNhap,
        pn.NgayNhap AS NgayNhap,
        pn.MaPhieuNhap AS MaPhieuNhap,
        pn.MaNV AS MaNV
    FROM 
        ChiTietPhieuNhap ctpn
    INNER JOIN 
        PhieuNhap pn ON ctpn.MaPhieuNhap = pn.MaPhieuNhap
    ORDER BY 
        pn.NgayNhap DESC;
END;
--13.Stored procedure Thêm nhân viên
GO
CREATE PROCEDURE sp_ThemNhanVien
    @MaNV VARCHAR(10),
    @HoTen NVARCHAR(100) ,
    @ChucVu NVARCHAR(50),
    @NgayVaoLam DATE,
    @SDT NVARCHAR(20),
    @Email NVARCHAR(100),
    @CCCD NVARCHAR(12)
AS
BEGIN
    INSERT INTO NhanVien (MaNV, HoTen, ChucVu, NgayVaoLam, SDT, Email, CCCD)
    VALUES (@MaNV, @HoTen, @ChucVu, @NgayVaoLam, @SDT, @Email, @CCCD)

END


--14.Stored procedure Sửa Nhân viên
GO
CREATE PROCEDURE sp_SuaNhanVien
    @MaNV VARCHAR(10),
    @HoTen NVARCHAR(100),
    @ChucVu NVARCHAR(50),
    @NgayVaoLam DATE,
    @SDT NVARCHAR(20),
    @Email NVARCHAR(100),
    @CCCD NVARCHAR(12)
AS
BEGIN
    UPDATE NhanVien
    SET 
        HoTen = @HoTen,
        ChucVu = @ChucVu,
        NgayVaoLam = @NgayVaoLam,
        SDT = @SDT,
        Email = @Email,
        CCCD = @CCCD
    WHERE MaNV = @MaNV
END

--15.Stored procedure Xoá Nhân Viên
GO
CREATE OR ALTER PROCEDURE sp_XoaNhanVien
    @MaNV VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    -- Xóa liên kết trong Hóa đơn
    UPDATE HoaDon
    SET MaNV = NULL
    WHERE MaNV = @MaNV;

    -- Xoá liên kết trong Phiếu Nhập
    UPDATE PhieuNhap
    SET MaNV = NULL
    WHERE MaNV = @MaNV;

    -- Xóa nhân viên
    DELETE FROM NhanVien
    WHERE MaNV = @MaNV;
END;

--16.Stored procedure Them khach hang
GO
CREATE PROCEDURE sp_ThemKhachHang
    @MaKH VARCHAR(10),
    @HoTen NVARCHAR(100),
    @DiaChi NVARCHAR(200),
    @SDT NVARCHAR(20),
    @Email NVARCHAR(100)
AS
BEGIN
    INSERT INTO KhachHang (MaKH, HoTen, DiaChi, SDT, Email)
    VALUES (@MaKH, @HoTen, @DiaChi, @SDT, @Email);
END;

--17.Stored procedure Thêm khuyến mãi
GO
CREATE OR ALTER PROCEDURE sp_ThemKhuyenMai
    @MaKhuyenMai VARCHAR(10),
    @MoTa NVARCHAR(255),
    @NgayBatDau DATE,
    @NgayKetThuc DATE,
    @PhanTramGiamGia INT
AS
BEGIN
    INSERT INTO KhuyenMai (MaKhuyenMai, MoTa, NgayBatDau, NgayKetThuc, PhanTramGiamGia)
    VALUES (@MaKhuyenMai, @MoTa, @NgayBatDau, @NgayKetThuc, @PhanTramGiamGia);
END;


--18.Stored procdure Cập nhật khuyến mãi
GO
CREATE PROCEDURE sp_CapNhatKhuyenMai
    @MaKhuyenMai VARCHAR(10),
    @MoTa NVARCHAR(255),
    @NgayBatDau DATE,
    @NgayKetThuc DATE,
    @PhanTramGiamGia INT
AS
BEGIN
    UPDATE KhuyenMai
    SET MoTa = @MoTa,
        NgayBatDau = @NgayBatDau,
        NgayKetThuc = @NgayKetThuc,
        PhanTramGiamGia = @PhanTramGiamGia
    WHERE MaKhuyenMai = @MaKhuyenMai;
END;
--19.Stored procedure Xoá khuyến mãi
GO
CREATE PROCEDURE sp_XoaKhuyenMai
    @MaKhuyenMai VARCHAR(10)
AS
BEGIN
    UPDATE KhuyenMai
    SET IsDeleted = 1  -- Đánh dấu là đã xoá (ẩn)
    WHERE MaKhuyenMai = @MaKhuyenMai;
END;

--20. Stored procedure Thêm hoá đơn và chi tiết hoá đơn
--**Tao type


GO
CREATE TYPE ChiTietHoaDonType AS TABLE
(
    MaSach VARCHAR(10), -- hoặc NVARCHAR nếu có ký tự Unicode
    SoLuong INT
);
-- **Tao stored procedure
GO
CREATE PROCEDURE sp_LapHoaDon
    @SoDienThoai VARCHAR(15),
    @MaNV VARCHAR(10),
    @MaKhuyenMai VARCHAR(10) = NULL,
    @ChiTiet ChiTietHoaDonType READONLY,
    @MaHD INT OUTPUT,
    @TenKH NVARCHAR(100) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MaKH VARCHAR(10),
            @TongTien DECIMAL(12,2),
            @TienGiam DECIMAL(12,2) = 0,
            @PhanTramGiam INT = 0;

    -- 1. Lấy khách hàng
    SELECT TOP 1 @MaKH = MaKH, @TenKH = HoTen
    FROM KhachHang
    WHERE SDT = @SoDienThoai;

    IF @MaKH IS NULL
    BEGIN
        RAISERROR(N'Không tìm thấy khách hàng có số điện thoại này.', 16, 1);
        RETURN;
    END

    -- 2. Kiểm tra tồn kho
    IF EXISTS (
        SELECT 1
        FROM @ChiTiet c
        JOIN Sach s ON c.MaSach = s.MaSach
        WHERE c.SoLuong > s.SoLuongTon
    )
    BEGIN
        RAISERROR(N'Một hoặc nhiều sách không đủ số lượng tồn.', 16, 1);
        RETURN;
    END

    -- 3. Tính tổng tiền từ Giá bìa của sách
    SELECT @TongTien = SUM(c.SoLuong * s.GiaBia)
    FROM @ChiTiet c
    JOIN Sach s ON c.MaSach = s.MaSach;

    -- 4. Lấy phần trăm giảm nếu có
    IF @MaKhuyenMai IS NOT NULL
    BEGIN
        SELECT @PhanTramGiam = PhanTramGiamGia
        FROM KhuyenMai
        WHERE MaKhuyenMai = @MaKhuyenMai;

        SET @TienGiam = @TongTien * @PhanTramGiam / 100.0;
    END

    -- 5. Thêm hóa đơn
    INSERT INTO HoaDon(NgayLap, TongTien, MaKH, MaNV, MaKhuyenMai, TongGiam, TrangThai)
    VALUES (GETDATE(), @TongTien, @MaKH, @MaNV, @MaKhuyenMai, @TienGiam, N'Đã thanh toán');

    -- 6. Lấy mã hóa đơn mới
    SET @MaHD = SCOPE_IDENTITY();

    -- 7. Thêm chi tiết hóa đơn
   -- 7. Thêm chi tiết hóa đơn
    INSERT INTO ChiTietHoaDon(MaHD, MaSach, SoLuong, DonGia)
    SELECT 
        @MaHD, c.MaSach, c.SoLuong, s.GiaBia
    FROM @ChiTiet c
    JOIN Sach s ON c.MaSach = s.MaSach;

    -- 8. Trừ số lượng tồn kho
    UPDATE s
    SET s.SoLuongTon = s.SoLuongTon - c.SoLuong
    FROM Sach s
    JOIN @ChiTiet c ON s.MaSach = c.MaSach;
END






--D. TẠO TRIGGER

--1.Trigger ghi log thêm
GO
CREATE TRIGGER trg_Log_Insert_NhanVien
ON NhanVien
AFTER INSERT
AS
BEGIN
    INSERT INTO LogNhanVien(MaNV, HanhDong, GhiChu)
    SELECT MaNV, N'Thêm', N'Thêm mới nhân viên'
    FROM inserted;
END;

--2.Trigger ghi log update
GO
CREATE TRIGGER trg_Log_Update_NhanVien
ON NhanVien
AFTER UPDATE
AS
BEGIN
    INSERT INTO LogNhanVien(MaNV, HanhDong, GhiChu)
    SELECT MaNV, N'Sửa', N'Cập nhật thông tin nhân viên'
    FROM inserted;
END;

--3. Trigger ghi log khi DELETE 
GO
CREATE TRIGGER trg_Log_Delete_NhanVien
ON NhanVien
AFTER DELETE
AS
BEGIN
    INSERT INTO LogNhanVien(MaNV, HanhDong, GhiChu)
    SELECT MaNV, N'Xoá', N'Xoá nhân viên khỏi hệ thống'
    FROM deleted;
END;

-- Trigger cập nhật tổng tiền 
GO
CREATE TRIGGER trg_CapNhatTongTien_AfterInsertUpdate
ON HoaDon
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Cập nhật lại tổng tiền của những khách hàng có thay đổi
    UPDATE k
    SET k.TongTien = ISNULL(h.TongTien, 0)
    FROM KhachHang k
    INNER JOIN (
        SELECT MaKH, SUM(ThanhTien) AS TongTien
        FROM HoaDon
        GROUP BY MaKH
    ) h ON k.MaKH = h.MaKH;
END;

--E. TẠO FUNCTION
--1. Function xem khuyến mãi --

GO
CREATE FUNCTION fn_GetMaKhuyenMaiHienTai()
RETURNS TABLE
AS
RETURN
    SELECT MaKhuyenMai, MoTa, NgayBatDau, NgayKetThuc,PhanTramGiamGia
    FROM KhuyenMai
    WHERE CAST(GETDATE() AS DATE) BETWEEN NgayBatDau AND NgayKetThuc;

-- F. CURSOR
-- Trong procedurse p_PhanLoaiKhachHang có sử dụng cursor để duyệt quả bảng Khách hàng
GO
CREATE OR ALTER PROCEDURE sp_PhanLoaiKhachHang
AS
BEGIN
    DECLARE @MaKH VARCHAR(10)
    DECLARE @TongTien DECIMAL(18,2)
    DECLARE @Hang NVARCHAR(20)

    DECLARE cur CURSOR FOR
        SELECT MaKH, TongTien FROM KhachHang;

    OPEN cur;
    FETCH NEXT FROM cur INTO @MaKH, @TongTien;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Chỉ phân loại dựa vào giá trị đã có
        IF @TongTien >= 10000000
            SET @Hang = N'VIP';
        ELSE IF @TongTien >= 2000000
            SET @Hang = N'Thường';
        ELSE
            SET @Hang = N'Mới';

        -- Cập nhật Hạng, KHÔNG cập nhật lại TongTien
        UPDATE KhachHang
        SET Hang = @Hang
        WHERE MaKH = @MaKH;

        FETCH NEXT FROM cur INTO @MaKH, @TongTien;
    END;

    CLOSE cur;
    DEALLOCATE cur;
END;

EXEC sp_PhanLoaiKhachHang;

-- CHƯƠNG 5: REPORT
-- 1. Báo cáo doanh thu theo tháng và năm
CREATE VIEW vw_DoanhSoBanHang_ThangNam AS
SELECT
    YEAR(NgayLap) AS Nam,
    MONTH(NgayLap) AS Thang,
    SUM(TongTien) AS TongDoanhThu,
    SUM(TongGiam) AS TongGiamGia,
    SUM(ThanhTien) AS DoanhThuSauGiam,
    COUNT(MaHD) AS SoHoaDon
FROM HoaDon
GROUP BY YEAR(NgayLap), MONTH(NgayLap)

-- 2. Báo cáo tổng hợp tồn kho sách
CREATE VIEW vw_TonKhoSach AS
SELECT 
    s.MaSach,
    s.TieuDe,
    s.SoLuongTon
FROM Sach s

-- 3. Báo cáo sách bán chạy 
CREATE VIEW vw_BestSeller_Sach AS
SELECT 
    s.MaSach,
    s.TieuDe,
    SUM(cthd.SoLuong) AS TongBan
FROM 
    ChiTietHoaDon cthd
    JOIN Sach s ON cthd.MaSach = s.MaSach
GROUP BY 
    s.MaSach, s.TieuDe

-- 4. Bao cáo tồn kho theo chi tiết nhập/xuất
CREATE VIEW vw_TonKhoChiTiet AS
SELECT 
    s.MaSach,
    s.TieuDe,
    ISNULL(SUM(n.SoLuong), 0) AS TongNhap,
    ISNULL(SUM(c.SoLuong), 0) AS TongBan,
    ISNULL(SUM(n.SoLuong), 0) - ISNULL(SUM(c.SoLuong), 0) AS TonKho
FROM 
    Sach s
    LEFT JOIN ChiTietPhieuNhap n ON s.MaSach = n.MaSach
    LEFT JOIN ChiTietHoaDon c ON s.MaSach = c.MaSach
GROUP BY 
    s.MaSach, s.TieuDe

-- 5. Báo cáo doanh số theo thể loại
CREATE VIEW vw_DoanhSo_TheLoai AS
SELECT 
    tl.MaTheLoai,
    tl.TenTheLoai,
    SUM(cthd.SoLuong) AS TongBan
FROM 
    ChiTietHoaDon cthd
    JOIN Sach_TheLoai stl ON cthd.MaSach = stl.MaSach
    JOIN TheLoai tl ON stl.MaTheLoai = tl.MaTheLoai
GROUP BY 
    tl.MaTheLoai, tl.TenTheLoai

-- 6. Báo cáo tổng hợp nhập sách theo tháng/năm
CREATE VIEW vw_NhapSach_ThangNam AS
SELECT
    YEAR(pn.NgayNhap) AS Nam,
    MONTH(pn.NgayNhap) AS Thang,
    SUM(ctpn.SoLuong) AS TongSoLuongNhap,
    SUM(ctpn.SoLuong * ctpn.GiaNhap) AS TongTienNhap
FROM 
    PhieuNhap pn
    JOIN ChiTietPhieuNhap ctpn ON pn.MaPhieuNhap = ctpn.MaPhieuNhap
GROUP BY 
    YEAR(pn.NgayNhap), MONTH(pn.NgayNhap)


-- 7. Báo cáo doanh số theo nhân viên
CREATE VIEW vw_DoanhSo_NhanVien AS
SELECT
    nv.MaNV,
    nv.HoTen,
    COUNT(hd.MaHD) AS SoHoaDon,
    SUM(hd.ThanhTien) AS TongDoanhSo
FROM 
    NhanVien nv
    LEFT JOIN HoaDon hd ON nv.MaNV = hd.MaNV
GROUP BY
    nv.MaNV, nv.HoTen

-- 8. Báo cáo doanh số theo khách hàng
CREATE VIEW vw_DoanhSo_KhachHang AS
SELECT
    kh.MaKH,
    kh.HoTen,
    kh.SDT,
    COUNT(hd.MaHD) AS SoLanMua,
    SUM(hd.ThanhTien) AS TongTienMua
FROM 
    KhachHang kh
    LEFT JOIN HoaDon hd ON kh.MaKH = hd.MaKH
GROUP BY
    kh.MaKH, kh.HoTen, kh.SDT

-- 10. Báo cáo khuyến mãi đang hoạt động
CREATE VIEW vw_KhuyenMai_HoatDong AS
SELECT 
    MaKhuyenMai,
    MoTa,
    NgayBatDau,
    NgayKetThuc,
    PhanTramGiamGia
FROM KhuyenMai
WHERE NgayBatDau <= CAST(GETDATE() AS DATE) 
  AND NgayKetThuc >= CAST(GETDATE() AS DATE) 
  AND isDeleted = 0


--F. PHÂN QUYỀN TÀI KHOẢN DTB
--1. Tạo login cho nhóm Nhân viên và Quản Lý, ReadOnly làm tài khoản mặc định
create login lg_readonly with password = 'ie103QLNS', DEFAULT_DATABASE = QLNS
create login lg_manager with password = 'ie103QLNS', DEFAULT_DATABASE = QLNS
create login lg_employee with password = 'ie103QLNS', DEFAULT_DATABASE = QLNS
--2. Tạo user cho từng login
create user us_readonly for login lg_readonly 
create user us_manager for login lg_manager 
create user us_employee for login lg_employee 
--3.Phân quyền user
alter role db_owner add member us_manager
alter role db_datareader add member us_readonly
alter role db_datareader add member us_employee
GRANT INSERT ON dbo.HOADON TO us_employee;
GO
GRANT INSERT ON dbo.CHITIETHOADON TO us_employee;
GO
GRANT INSERT ON dbo.KHACHHANG TO us_employee;
GO
GRANT UPDATE ON dbo.KhuyenMai TO us_employee;
GO
GRANT EXECUTE ON OBJECT::dbo.sp_TimKiemSach_TuKhoa TO us_employee;
GO
GRANT EXECUTE ON OBJECT::dbo.sp_CapNhatKhuyenMai TO us_employee;
GO
GRANT EXECUTE ON OBJECT::dbo.sp_XoaKhuyenMai TO us_employee;
GO
GRANT EXECUTE ON OBJECT::dbo.sp_LapHoaDon TO us_employee;
GO
GRANT EXECUTE ON TYPE::dbo.ChiTietHoaDonType TO us_employee;
GO
GRANT EXECUTE ON OBJECT::dbo.sp_ThemKhachHang TO us_employee;
GO

