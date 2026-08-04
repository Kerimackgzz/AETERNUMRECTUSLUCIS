# Conflict Log

## 2026-08-04 — Foundation sırasında harici Ajan 2 çıktıları

- Gözlem: Kök `wwwroot/*` ağacı ile `docs/agents/reports/claude-home-hero-report.md`, Ajan 3 çalışırken bu oturum dışında oluştu.
- Karar: Dosyalar korunmuş, okunmamış/değiştirilmemiş ve Ajan 3 commit’lerinden dışlanmıştır.
- Risk: Orkestrasyona göre Ajan 2 foundation integration’a alınmadan başlamamalıdır.
- Coordinator aksiyonu: Kaynağı doğrula; foundation merge’i sonrasında ilgili branch/worktree üzerinden kontrollü taşı veya yeniden üret.
