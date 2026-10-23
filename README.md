# N1736 Shop – Quản lý bán hàng

Ứng dụng ASP.NET Core MVC quản lý cửa hàng công nghệ, giỏ hàng, đặt hàng và trang quản trị đơn hàng. Dữ liệu dùng SQL Server Express (`.\SQLEXPRESS`, database `TechHavenSales`); EF Core tự áp dụng migration và tạo bốn sản phẩm mẫu khi chạy lần đầu.

## Chạy ứng dụng

1. Cài .NET 10 SDK và SQL Server Express; bảo đảm dịch vụ SQL Server Express đang chạy.
2. (Tùy chọn) Tạo database và dữ liệu mẫu trước bằng file SQL:

   ```powershell
   sqlcmd -S ".\SQLEXPRESS" -E -C -i .\create-database.sql
   ```

   File này có thể chạy lại an toàn; ứng dụng cũng tự áp dụng migration khi khởi động.
3. Mở terminal tại thư mục dự án và cấu hình tài khoản quản trị bằng User Secrets:

   ```powershell
   dotnet user-secrets set "Admin:Username" "admin"
   dotnet user-secrets set "Admin:Password" "Pass_1234"
   ```

   Thay mật khẩu mẫu bằng mật khẩu mạnh của riêng bạn. Không lưu mật khẩu vào mã nguồn.
4. Chạy ứng dụng:

   ```powershell
   dotnet run
   ```

   Cửa hàng mở tại URL được in trong terminal. Đăng nhập quản trị tại `/AdminAccount/Login`; quản lý đơn hàng ở `/Admin/Orders` và quản lý sản phẩm ở `/Admin/Products`.

Admin có thể thêm, sửa, xóa sản phẩm. Ảnh sản phẩm tải trực tiếp tại trang quản trị, chỉ nhận JPG/PNG/WebP tối đa 5 MB. Sản phẩm đã được dùng trong đơn hàng không thể xóa để bảo toàn lịch sử.

Nếu chưa cấu hình `Admin:Username` và `Admin:Password`, ứng dụng vẫn khởi động nhưng không thể tạo tài khoản quản trị.

## Kiểm thử

```powershell
dotnet test .\final.Tests\final.Tests.csproj
```
