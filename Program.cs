using System.Text;

Console.OutputEncoding = Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==========================================================================");
Console.WriteLine("  RACE CONDITION & DOUBLE-SPEND (ÇİFT HARCAMA) SİMÜLASYONU");
Console.WriteLine("  Senaryo: Başlangıç bakiyesi 100 TL olan hesaptan aynı anda");
Console.WriteLine("           10 paralel işlem 100'er TL çekmeye çalışıyor.");
Console.WriteLine("==========================================================================");
Console.ResetColor();
Console.WriteLine();

// -----------------------------------------------------------------------------
// 1. SENARYO: GÜVENSİZ (NAIVE) KOD - RACE CONDITION OLUŞUR
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine(">>> 1. SENARYO ÇALIŞTIRILIYOR: Güvensiz (Thread-Unsafe) Banka Hesabı");
Console.ResetColor();

var unsafeAccount = new UnsafeAccount(initialBalance: 100m);
int unsafeSuccessCount = 0;
int unsafeFailedCount = 0;

var unsafeTasks = Enumerable.Range(1, 10).Select(async i =>
{
    bool success = await unsafeAccount.WithdrawAsync(amount: 100m);
    if (success)
    {
        Interlocked.Increment(ref unsafeSuccessCount);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [İşlem #{i:D2}] 100 TL Çekildi! (Kalan Bakiye: {unsafeAccount.Balance} TL)");
        Console.ResetColor();
    }
    else
    {
        Interlocked.Increment(ref unsafeFailedCount);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [İşlem #{i:D2}] Yetersiz Bakiye Nedeniyle Reddedildi.");
        Console.ResetColor();
    }
});

await Task.WhenAll(unsafeTasks);

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("  [KRİTİK GÜVENLİK AÇIĞI TESPİT EDİLDİ - FAIL]");
Console.WriteLine($"  * Başlangıç Bakiyesi     : 100 TL");
Console.WriteLine($"  * Çekilmek İstenen Toplam: 1.000 TL (10 x 100 TL)");
Console.WriteLine($"  * Başarılı İşlem Sayısı  : {unsafeSuccessCount}/10");
Console.WriteLine($"  * Son Bakiye             : {unsafeAccount.Balance} TL");
Console.WriteLine("  * SONUÇ                  : Çift Harcama (Double-Spend) Felaketi!");
Console.WriteLine("                             Kullanıcı 100 TL ile kasadan 1.000 TL çekti!");
Console.ResetColor();

Console.WriteLine();
Console.WriteLine("--------------------------------------------------------------------------");
Console.WriteLine();

// -----------------------------------------------------------------------------
// 2. SENARYO: GÜVENLİ (THREAD-SAFE / CONCURRENCY SAFE) KOD
// -----------------------------------------------------------------------------
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine(">>> 2. SENARYO ÇALIŞTIRILIYOR: Eşzamanlılık Korumalı (Thread-Safe) Banka Hesabı");
Console.ResetColor();

var safeAccount = new SafeAccount(initialBalance: 100m);
int safeSuccessCount = 0;
int safeFailedCount = 0;

var safeTasks = Enumerable.Range(1, 10).Select(async i =>
{
    bool success = await safeAccount.WithdrawAsync(amount: 100m);
    if (success)
    {
        Interlocked.Increment(ref safeSuccessCount);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [İşlem #{i:D2}] 100 TL Çekildi! (Kalan Bakiye: {safeAccount.Balance} TL) -> ONAYLANDI");
        Console.ResetColor();
    }
    else
    {
        Interlocked.Increment(ref safeFailedCount);
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  [İşlem #{i:D2}] Yetersiz Bakiye! İşlem güvenli şekilde ENGELLENDİ.");
        Console.ResetColor();
    }
});

await Task.WhenAll(safeTasks);

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("  [KUSURSUZ KORUMA - SUCCESS]");
Console.WriteLine($"  * Başlangıç Bakiyesi     : 100 TL");
Console.WriteLine($"  * Başarılı İşlem Sayısı  : {safeSuccessCount}/10 (Yalnızca ilk gelen işlem)");
Console.WriteLine($"  * Engellenen İşlem Sayısı: {safeFailedCount}/10");
Console.WriteLine($"  * Son Bakiye             : {safeAccount.Balance} TL");
Console.WriteLine("  * SONUÇ                  : Bakiye tam korundu. Race Condition engellendi.");
Console.ResetColor();

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==========================================================================");
Console.WriteLine("  Simülasyon tamamlandı.");
Console.WriteLine("==========================================================================");
Console.ResetColor();


// =============================================================================
// MODELLER VE İŞ MANTIKLARI
// =============================================================================

/// <summary>
/// HATALI UYGULAMA:
/// if (balance >= amount) kontrolü ile bakiye düşümü arasında geçen sürede
/// diğer thread'ler de şartı doğru görerek aynı parayı mükerrer çeker.
/// </summary>
public class UnsafeAccount
{
    public decimal Balance { get; private set; }

    public UnsafeAccount(decimal initialBalance)
    {
        Balance = initialBalance;
    }

    public async Task<bool> WithdrawAsync(decimal amount)
    {
        // 1. Kontrol (Check)
        if (Balance >= amount)
        {
            // Veritabanı gecikmesi veya I/O operasyonu simülasyonu
            // Bu gecikme sırasında diğer 9 işlem de Balance'ı henüz 100 TL olarak okur!
            await Task.Delay(Random.Shared.Next(15, 30));

            // 2. Eylem (Act) - Race condition tam burada patlar!
            Balance -= amount;
            return true;
        }

        return false;
    }
}

/// <summary>
/// GÜVENLİ UYGULAMA:
/// Eşzamanlı istekleri asenkron kilit mekanizması (SemaphoreSlim) ile sıralar.
/// Kontrol ve bakiye düşümü atomik bir blok (Critical Section) haline getirilir.
/// </summary>
public class SafeAccount
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    public decimal Balance { get; private set; }

    public SafeAccount(decimal initialBalance)
    {
        Balance = initialBalance;
    }

    public async Task<bool> WithdrawAsync(decimal amount)
    {
        // Yalnızca 1 thread'in kritik alana girmesine izin verilir
        await _lock.WaitAsync();
        try
        {
            // Kontrol ve düşüm atomik olarak gerçekleşir
            if (Balance >= amount)
            {
                await Task.Delay(Random.Shared.Next(15, 30));
                Balance -= amount;
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
