using Xunit;

// WebApplicationFactory<Program>'ın birden fazla alt sınıfı (HealthCheckTests'in
// varsayılan factory'si ve AuthWebApplicationFactory) aynı process içinde paralel
// başlatıldığında .NET'in HostFactoryResolver diagnostic listener'ı yarış durumuna
// girip "entry point exited without ever building an IHost" ile flaky şekilde
// başarısız olabiliyor. Bu entegrasyon testleri zaten paylaşılan bir PostgreSQL'e
// yazdığından, koleksiyonları sıralı çalıştırmak hem bu sorunu çözer hem de veri
// çakışma riskini azaltır.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
