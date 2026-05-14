=================================================================
HƯỚNG DẪN CÀI ZEBRA SCANNER SDK VÀ COPY DLL
=================================================================

ĐỂ BẬT TÍNH NĂNG BARCODE (ZEBRA SCANNER):
------------------------------------------
1. Tải và cài đặt "Zebra Scanner SDK" từ:
   https://www.zebra.com/us/en/support-downloads/software/developer-tools/scanner-sdk-for-windows.html

2. Sau khi cài xong, tìm file Interop.CoreScanner.dll tại:
   C:\Program Files\Zebra Technologies\Barcode Scanners\Scanner SDK\
   Scanner SDK\Sample Applications\bin\Interop.CoreScanner.dll

3. Copy file đó vào thư mục NÀY (ScanAndScale.Core\lib\):
   → ScanAndScale.Core\lib\Interop.CoreScanner.dll

4. Rebuild project ScanAndScale.Core
   → MSBuild sẽ tự phát hiện DLL và bật symbol ZEBRA_SCANNER
   → BarcodeDriver sẽ hoạt động đầy đủ

NẾU KHÔNG CÓ ZEBRA SDK:
------------------------
- Project vẫn build được bình thường
- BarcodeDriver.Initialize() sẽ trả về false
- BarcodeDriver.IsZebraSdkAvailable == false
- Tính năng RFID và Scale vẫn hoạt động bình thường

=================================================================
