using Resilite;

Console.WriteLine("==================================================");
Console.WriteLine("       RESİLİTE KÜTÜPHANESİ - SENARYO TESTLERİ    ");
Console.WriteLine("==================================================\n");

// 1. Senaryo: Temel Zaman Aşımı (Timeout) Başarı ve Başarısızlık Durumları
await Scenario_Timeout_VariationsAsync();

Console.WriteLine("\n--------------------------------------------------\n");

// 2. Senaryo: Üstel Artan Bekleme (Exponential Backoff) ile Retry Testi
await Scenario_Retry_ExponentialBackoffAsync();

Console.WriteLine("\n--------------------------------------------------\n");

// 3. Senaryo: Circuit Breaker'ın Tam Yaşam Döngüsü (Closed -> Open -> Half-Open -> Closed)
await Scenario_CircuitBreaker_FullLifecycleAsync();

Console.WriteLine("\n--------------------------------------------------\n");

// 4. Senaryo: Tüm Kuralların Birlikte Çalıştığı Boru Hattı (Pipeline) Entegrasyonu
await Scenario_CombinedPipeline_SuccessAndFailureAsync();

Scenario_SynchronousAndProtocolUsage();

Console.WriteLine("\n==================================================");
Console.WriteLine("       TÜM SENARYO TESTLERİ TAMAMLANDI            ");
Console.WriteLine("==================================================");




/// <summary>
/// SENARYO 1: Zaman aşımı sınırları içinde biten işler başarılı döner, aşanlar TimeoutException fırlatır.
/// </summary>
static async Task Scenario_Timeout_VariationsAsync()
{
    Console.WriteLine(">>> [SENARYO 1] Zaman Aşımı (Timeout) Varyasyonları Test Ediliyor...");

    var pipeline = new ResiliencePipelineBuilder()
        .AddTimeout(TimeSpan.FromSeconds(1)) // 1 saniye sınır
        .Build();

    // Durum A: Süreye uyan işlem (Başarılı olmalı)
    try
    {
        Console.WriteLine("[1.A] Hızlı işlem çalıştırılıyor (0.3 sn)...");

        var result = await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(300, ct);

            return "İşlem Zamanında Bitti";
        });

        Console.WriteLine($"[BAŞARILI]: {result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[BEKLENMEYEN HATA]: {ex.Message}");
    }

    // Durum B: Süreyi aşan işlem (Timeout fırlatmalı)
    try
    {
        Console.WriteLine("\n[1.B] Yavaş işlem çalıştırılıyor (2 sn)...");

        await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(2000, ct);

            return "Bu çalışmamalı";
        });
    }
    catch (ResilienceTimeoutException ex)
    {
        Console.WriteLine($"[BAŞARILI YAKALAMA]: {ex.Message}");
    }
}

/// <summary>
/// SENARYO 2: Retry politikasının başarısız denemelerden sonra başarıya ulaşması ve backoff süresi.
/// </summary>
static async Task Scenario_Retry_ExponentialBackoffAsync()
{
    Console.WriteLine(">>> [SENARYO 2] Retry ve Exponential Backoff Test Ediliyor...");

    // 4 deneme hakkı, başlangıç gecikmesi 200ms
    var pipeline = new ResiliencePipelineBuilder()
        .AddRetry(
            maxRetryAttempts: 4,
            delay: TimeSpan.FromMilliseconds(200),
            useExponentialBackoff: true)
        .Build();

    int attempt = 0;

    try
    {
        var data = await pipeline.ExecuteAsync(async ct =>
        {
            attempt++;

            Console.WriteLine($"-> Donanım bağlantı denemesi #{attempt} yapılıyor...");

            if (attempt < 4)
            {
                // İlk 3 denemede geçici G/Ç hatası fırlatalım
                throw new IOException($"Port hatası - Deneme {attempt}");
            }

            return "Veri Akışı Sağlandı!";
        });

        Console.WriteLine($"[SONUÇ BAŞARILI]: {data} (Toplam {attempt}. denemede)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HATA]: {ex.Message}");
    }
}

/// <summary>
/// SENARYO 3: Circuit Breaker'ın kapanması, açılması ve tetiklenme mekanizması.
/// </summary>
static async Task Scenario_CircuitBreaker_FullLifecycleAsync()
{
    Console.WriteLine(">>> [SENARYO 3] Circuit Breaker (Devre Kesici) Yaşam Döngüsü...");

    // 2 hatada devreyi aç, 3 saniye açık tut
    var pipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(3))
        .Build();

    // 1. İstek: Başarısız olacak (Hata sayacı: 1)
    try
    {
        await pipeline.ExecuteAsync<string>(async ct => throw new IOException("Cihaz Arızalı 1"));
    }
    catch (Exception ex) { Console.WriteLine($"[1. İstek Hatası]: {ex.Message} (Devre Kapalı / Closed)"); }

    // 2. İstek: Başarısız olacak (Hata sayacı: 2 -> Eşik değere ulaşıldı, DEVRE AÇILACAK!)
    try
    {
        await pipeline.ExecuteAsync<string>(async ct => throw new IOException("Cihaz Arızalı 2"));
    }
    catch (Exception ex) { Console.WriteLine($"[2. İstek Hatası]: {ex.Message} (Eşik aşıldı, devre AÇILDI!)"); }

    // 3. İstek: Devre açık olduğu için fonksiyona hiç girmeden anında reddedilmeli (Fail-Fast)
    try
    {
        Console.WriteLine("\n[3. İstek Gönderiliyor - Devre AÇIK durumdayken]...");
        await pipeline.ExecuteAsync<string>(async ct =>
        {
            Console.WriteLine("ÇOK ÖNEMLİ: Bu satır yazdırılmamalı, çünkü devre açık!");
            return "Çalıştı";
        });
    }
    catch (CircuitBrokenException ex)
    {
        Console.WriteLine($"[KORUMA DEVREYE GİRDİ]: {ex.Message}");
    }

    // 4. Bekleme: Devrenin tekrar test moduna (Half-Open) geçmesi için 3.5 saniye bekleyelim
    Console.WriteLine("\n[Bekleniyor] Devrenin Half-Open durumuna geçmesi için 3.5 saniye bekleniyor...");
    await Task.Delay(3500);

    // 5. İstek: Half-Open durumunda başarılı bir istek atıp devreyi tekrar kapatacağız (Closed)
    try
    {
        Console.WriteLine("[5. İstek Gönderiliyor - Half-Open durumunda test]...");
        var result = await pipeline.ExecuteAsync<string>(async ct =>
        {
            Console.WriteLine("Cihaz iyileşti, yanıt veriyor...");
            return "Sistem Stabil";
        });
        Console.WriteLine($"[BAŞARILI YANIT]: {result} (Devre başarıyla kapandı / Closed)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Hata]: {ex.Message}");
    }
}

/// <summary>
/// SENARYO 4: Timeout + Retry + Circuit Breaker üçlüsünün kusursuz sarmallama (Pipeline) entegrasyonu.
/// </summary>
static async Task Scenario_CombinedPipeline_SuccessAndFailureAsync()
{
    Console.WriteLine(">>> [SENARYO 4] Tam Kombine Boru Hattı Testi (Timeout + Retry + Circuit Breaker)...");

    // Kurulum: 1.5 sn timeout, 2 retry hakkı, 3 hata eşikli devre kesici
    var pipeline = new ResiliencePipelineBuilder()
        .AddTimeout(TimeSpan.FromSeconds(1.5))
        .AddRetry(
            maxRetryAttempts: 2, 
            delay: TimeSpan.FromMilliseconds(100))
        .AddCircuitBreaker(
            failureThreshold: 3, 
            breakDuration: TimeSpan.FromSeconds(5))
        .Build();

    int callCount = 0;

    // Bu simülasyonda ilk 2 deneme yavaş çalışıp timeout'a takılacak, 3. denemede ise anında başarılı olacak.
    try
    {
        Console.WriteLine("Kombine boru hattı üzerinden işlem tetikleniyor...");

        var response = await pipeline.ExecuteAsync(async ct =>
        {
            callCount++;

            if (callCount < 3)
            {
                Console.WriteLine($"-> Deneme #{callCount}: İşlem yavaşlatılıyor (Timeout tetiklenecek)...");

                await Task.Delay(2000, ct); // 1.5 sn timeout sınırını aşacak
            }

            Console.WriteLine($"-> Deneme #{callCount}: İşlem hızla tamamlanıyor.");
            
            return "Kombine Başarı Raporu";
        });

        Console.WriteLine($"[SONUÇ]: {response} (Toplam çağrı/deneme: {callCount})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Beklenmeyen Hata]: {ex.GetType().Name} -> {ex.Message}");
    }
}



/// <summary>
/// SENARYO 5: Senkron (Sync) metotlar ve farklı protokol simülasyonları testi.
/// </summary>
static void Scenario_SynchronousAndProtocolUsage()
{
    Console.WriteLine(">>> [SENARYO 5] Senkron Metot ve Protokol Entegrasyon Testi...");

    // Boru hattımızı kuruyoruz (Retry ve Timeout içeren akış)
    var pipeline = new ResiliencePipelineBuilder()
        .AddRetry(maxRetryAttempts: 3, delay: TimeSpan.FromMilliseconds(100))
        .Build();

    int syncAttempt = 0;

    try
    {
        // Senkron Değer Döndüren Metot Testi (.Execute<T>)
        var syncResult = pipeline.Execute(() =>
        {
            syncAttempt++;

            Console.WriteLine($"-> Senkron işlem çalıştırılıyor... (Deneme: {syncAttempt})");

            if (syncAttempt < 3)
            {
                throw new IOException("Erişim reddedildi, tekrar deneniyor...");
            }

            return "Senkron Veri Okundu";
        });

        Console.WriteLine($"[SENARYO 5 BAŞARILI]: {syncResult} (Toplam Deneme: {syncAttempt})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Senkron Hata]: {ex.Message}");
    }

    try
    {
        Console.WriteLine("\n-> Senkron void işlem tetikleniyor...");

        pipeline.Execute(() =>
        {
            Console.WriteLine("Senkron loglama veya dosya yazma işlemi gerçekleştirildi.");
        });

        Console.WriteLine("[SENARYO 5.2 BAŞARILI]: Void işlem hatasız tamamlandı.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Void Hata]: {ex.Message}");
    }
}