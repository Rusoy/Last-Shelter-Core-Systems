# Last Shelter Protocol - Core Systems

Bu depo, Google Play Store'da yayınlanan **Last Shelter Protocol** isimli mobil hayatta kalma oyununun temel mühendislik sistemlerini barındırmaktadır. Kodlar, temiz kod (Clean Code) prensiplerine, optimizasyona ve gelecekteki çok oyunculu (multiplayer) mimari planlarına uygun olarak tasarlanmıştır.

## 📌 Öne Çıkan Sistemler

### 1. Asenkron Envanter ve Veritabanı Yönetimi (`InventoryManager.cs`)
* **Firebase Entegrasyonu:** Oyuncu ve NPC envanter verileri lokal cihaz yerine, asenkron (`Async/Await`) işlemlerle Firebase bulut sistemine kaydedilir. Bu sayede hile (anti-cheat) koruması ve bulut kaydı (Cloud Save) sağlanmıştır.
* **Modüler Veri Yapısı:** Tüm sahnelerdeki objeler dinamik olarak taranıp, `Dictionary` yapıları ile JSON benzeri bir formata çevrilerek veri tabanına işlenir. Singleton pattern kullanılarak sahneler arası veri taşınımı güvenceye alınmıştır.

### 2. Optimize Edilmiş Savaş Mekaniği (`PlayerCombat.cs`)
* **Sıfır Garbage Collection:** Çarpışma ve fizik aramalarında standart metotlar yerine `Physics.OverlapSphereNonAlloc` kullanılarak bellek tahsisi (Memory Allocation) sıfıra indirilmiş ve mobil cihazlarda yüksek FPS hedeflenmiştir.
* **Failsafe (Güvenlik) Mimarisi:** Animasyon event'lerinin motor kaynaklı atlanması ihtimaline karşı `Invoke` tabanlı yedek güvenlik kilitleri yazılarak sistemin hata toleransı artırılmıştır.
