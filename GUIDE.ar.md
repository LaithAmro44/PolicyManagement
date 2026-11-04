# تشغيل المشروع وفهمه

## التشغيل أول مرة

تحتاج Windows و.NET SDK 10.0.401 أو أحدث ضمن .NET 10، وSQL Server LocalDB. افتح Terminal داخل مجلد المشروع الذي يحتوي `PolicyManagement.sln`.

```powershell
dotnet restore
dotnet tool restore
dotnet ef database update --project src/PolicyManagement.Data --startup-project src/PolicyManagement.Data
dotnet build PolicyManagement.sln -c Release
dotnet test tests/PolicyManagement.Tests -c Release
```

الـmigration ينشئ الجداول وشركتين: Bank A وBank B، ولكل شركة سياستان.

شغّل الويب:

```powershell
dotnet run --project src/PolicyManagement.Admin --launch-profile http
```

افتح `http://localhost:5101`. Swagger على `http://localhost:5101/swagger`.

اترك الويب يعمل، وافتح Terminal ثانية داخل مجلد المشروع:

```powershell
dotnet run --project src/PolicyManagement.Desktop
```

لإيقاف الويب اضغط `Ctrl+C` داخل Terminal الخاصة به. أغلق WPF من النافذة.

إذا استخدمت SQL Server مختلفاً، غيّر `ConnectionStrings:PolicyDatabase` في `src/PolicyManagement.Admin/appsettings.json`، وحدد نفس الاتصال لأمر EF:

```powershell
$env:POLICYMANAGEMENT_CONNECTION_STRING = 'Server=YOUR_SERVER;Database=PolicyManagement;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef database update --project src/PolicyManagement.Data --startup-project src/PolicyManagement.Data
```

## تجربة قصيرة

1. في الويب اختر Bank A، واضغط New policy. جرّب الحفظ بدون اسم لترى التحقق.
2. أنشئ سياسة باسم واضح، ثم عدّلها. الاسم لازم يكون فريداً داخل الشركة نفسها.
3. في WPF اختر Bank A، واضغط Refresh list. اختر صفاً واضغط Load selected details.
4. اضغط Open live events، ثم Connect / reconnect.
5. عدّل سياسة Bank A من الويب. يظهر PolicyUpdated في نافذة الأحداث.
6. من نافذة WPF الرئيسية اختر Bank B، وافتح نافذة أحداث ثانية واتصل.
7. عدّل Bank A مرة أخرى. نافذة Bank A تستقبل الحدث؛ نافذة Bank B لا تستقبله.
8. حذف سياسة من الويب يطلب تأكيداً ويرسل PolicyDeleted.

كل نافذة أحداث تحفظ الشركة التي فتحت لها. تغيير الشركة في النافذة الرئيسية لا يغيّر شركة نافذة الأحداث المفتوحة.

## ترتيب قراءة الكود

| الملف | الفكرة |
|---|---|
| `src/PolicyManagement.Data/Policy.cs` و`Tenant.cs` | الجداول وحقول السياسة والشركة |
| `src/PolicyManagement.Data/ITenantContext.cs` و`TenantContext.cs` | تخزين الشركة الحالية داخل نطاق الطلب أو جلسة Blazor |
| `src/PolicyManagement.Data/PolicyDbContext.cs` | العلاقات، الـquery filter، ختم TenantId والتواريخ عند الحفظ |
| `src/PolicyManagement.Admin/Services/AdminPolicyService.cs` | إنشاء وتعديل وحذف، فحص التكرار، ثم إرسال الحدث |
| `src/PolicyManagement.Admin/Components/Pages/Policies.razor` | الجدول، اختيار الشركة، نموذج الإدخال |
| `src/PolicyManagement.Admin/Infrastructure/TenantResolutionMiddleware.cs` | قراءة X-Tenant-Id والتحقق من وجود الشركة |
| `src/PolicyManagement.Admin/Controllers/PoliciesController.cs` | GET للقائمة وGET لسياسة واحدة |
| `src/PolicyManagement.Admin/Infrastructure/PolicyWebSocketHandler.cs` | فتح الاتصال وربطه بشركة وإزالته عند الانقطاع |
| `src/PolicyManagement.Admin/Infrastructure/PolicySocketHub.cs` | تجميع الاتصالات حسب الشركة وإرسال الأحداث |
| `src/PolicyManagement.Contracts/PolicyDto.cs` و`PolicyEventMessage.cs` | شكل البيانات المرسلة للعميل |
| `src/PolicyManagement.Desktop/ViewModels/ApiWindowViewModel.cs` | HTTP، عرض القائمة والتفاصيل، تجاهل ردود الشركة السابقة |
| `src/PolicyManagement.Desktop/ViewModels/EventsWindowViewModel.cs` | WebSocket، حالة الاتصال، استقبال وعرض الأحداث |

## اشرح التصميم بهذه الأفكار

- جمعت Blazor وAPI وWebSocket في host واحد. هيك الحفظ يرسل الحدث مباشرة بدون خدمة وسيطة.
- البيانات في مكتبة مشتركة. نفس DbContext ونفس entities لكل أجزاء السيرفر.
- القراءة معزولة مركزياً بالـglobal query filter. لا أكرر شرط الشركة في كل استعلام.
- الإدخال يأخذ TenantId من سياق السيرفر. التعديل والحذف يتحققان من الشركة الأصلية.
- TenantId أيضاً concurrency token؛ يدخل في شرط SQL للتعديل والحذف، فيمنع تعديل صف شركة أخرى عبر entity منفصلة.
- DTO يحدد البيانات العامة ويرجع عقداً واضحاً بدل علاقات EF وبيانات التتبع.
- الأحداث ترسل بعد نجاح الحفظ. لكل socket قفل إرسال، والعميل البطيء له مهلة ثانيتين.
- WPF يستخدم MVVM صغيراً وasync/await، بدون مكتبة إضافية.
- اختيار الشركة هنا للتجربة. الإنتاج يحتاج authentication وربط الشركة بهوية موثوقة؛ الـheader وحده ليس صلاحية دخول.
- الأحداث غير محفوظة لإعادة التشغيل، والسيرفر مصمم لنسخة واحدة. هذه حدود مقصودة لحجم التكليف.

## الفحص المنفذ

- Release build ناجح، بدون أخطاء أو تحذيرات.
- 12 اختباراً آلياً ناجحاً. اختبارات البيانات تستخدم EF InMemory.
- فحص منفصل على LocalDB جديدة: migrations وCRUD ورفض الاسم المكرر وعزل الشركات.
- HTTP فعلي: القائمة والتفاصيل والفلتر، و404 لمعرّف شركة أخرى، و400 للشركة الناقصة أو غير الصحيحة.
- WebSocket فعلي: استقبال Created وUpdated وDeleted لشركة A، وعدم وصولها لشركة B.
- مراجعة شكل الويب والتحقق من الاسم المطلوب. فُتحت نافذة WPF الرئيسية ونافذة الأحداث بعد إصلاح binding كان يغلق التطبيق.
- اكتمل فحص WPF اليدوي لاحقاً: تحميل قائمة Bank A وتفاصيل سياسة، تبديل الشركة ومسح البيانات السابقة، وتحميل قائمة Bank B المختلفة.
- فُتحت نافذتا أحداث للشركتين. تعديل Bank A من Blazor وصل لنافذة A فقط، وتعديل Bank B وصل لنافذة B فقط.
- قُطع اتصال نافذة B وأُعيد بنجاح، ثم استقبلت حدث تعديل جديداً. فحص الإنشاء والحذف وأحداثهما تم آلياً على SQL وWebSocket كما ورد أعلاه.
