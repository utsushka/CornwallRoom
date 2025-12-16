namespace CornwallRoom;

/// <summary>
/// Список объектов сцены для коллективного поиска пересечений.
/// Реализует интерфейс IHittable для единообразной обработки сложных сцен.
/// </summary>
public sealed class HittableList : IHittable
{
    private readonly List<IHittable> _items = new();

    public void Add(IHittable h) => _items.Add(h);

    /// <summary>
    /// Находит ближайшее пересечение луча с любым объектом в списке.
    /// Использует алгоритм поиска ближайшего пересечения для оптимизации.
    /// </summary>
    public bool Hit(in Ray ray, double tMin, double tMax, out HitRecord hit)
    {
        hit = default;
        bool any = false;
        double closest = tMax;

        foreach (var obj in _items)
        {
            if (obj.Hit(ray, tMin, closest, out var temp))
            {
                any = true;
                closest = temp.T;  // Обновляем ближайшее расстояние
                hit = temp;
            }
        }
        return any;
    }
}

/// <summary>
/// Виртуальная камера для генерации лучей через воображаемую плоскость изображения.
/// Реализует простую модель камеры с перспективной проекцией.
/// </summary>
public sealed class Camera
{
    private readonly Vec3 _origin;
    private readonly Vec3 _horizontal;
    private readonly Vec3 _vertical;
    private readonly Vec3 _lowerLeftCorner;

    /// <summary>
    /// Создает камеру с заданными параметрами позиции и ориентации.
    /// </summary>
    /// <param name="lookFrom">Позиция камеры в мировых координатах</param>
    /// <param name="lookAt">Точка, на которую смотрит камера</param>
    /// <param name="vUp">Вектор "вверх" для определения ориентации камеры</param>
    /// <param name="vfovDeg">Вертикальное поле зрения в градусах</param>
    /// <param name="aspect">Соотношение сторон (ширина/высота)</param>
    public Camera(Vec3 lookFrom, Vec3 lookAt, Vec3 vUp, double vfovDeg, double aspect)
    {
        // Вычисление размеров плоскости изображения
        double theta = vfovDeg * Math.PI / 180.0;
        double h = Math.Tan(theta / 2.0);
        double viewportHeight = 2.0 * h;
        double viewportWidth = aspect * viewportHeight;

        // Ортонормированный базис камеры
        Vec3 w = (lookFrom - lookAt).Normalized();  // Направление "вперед" (к сцене)
        Vec3 u = Vec3.Cross(vUp, w).Normalized();   // Направление "вправо"
        Vec3 v = Vec3.Cross(w, u);                  // Направление "вверх"

        _origin = lookFrom;
        _horizontal = viewportWidth * u;
        _vertical = viewportHeight * v;
        _lowerLeftCorner = _origin - _horizontal / 2.0 - _vertical / 2.0 - w;
    }

    /// <summary>
    /// Генерирует луч через заданную точку на плоскости изображения.
    /// </summary>
    /// <param name="s">Горизонтальная координата (0..1)</param>
    /// <param name="t">Вертикальная координата (0..1)</param>
    /// <returns>Луч из камеры через точку на плоскости изображения</returns>
    public Ray GetRay(double s, double t)
    {
        Vec3 dir = (_lowerLeftCorner + s * _horizontal + t * _vertical - _origin).Normalized();
        return new Ray(_origin, dir);
    }
}

/// <summary>
/// Цвет в линейном RGB пространстве.
/// Хранит компоненты как числа с плавающей точкой для точных вычислений.
/// Конвертируется в sRGB с гамма-коррекцией для отображения.
/// </summary>
public readonly struct ColorRGB
{
    public readonly double R;
    public readonly double G;
    public readonly double B;

    public ColorRGB(double r, double g, double b) { R = r; G = g; B = b; }

    // Арифметические операции для удобства вычислений
    public static ColorRGB operator +(ColorRGB a, ColorRGB b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public static ColorRGB operator *(ColorRGB a, double t) => new(a.R * t, a.G * t, a.B * t);
    public static ColorRGB operator *(double t, ColorRGB a) => a * t;
    public static ColorRGB operator *(ColorRGB a, ColorRGB b) => new(a.R * b.R, a.G * b.G, a.B * b.B);

    public static readonly ColorRGB Black = new(0, 0, 0);
    public static readonly ColorRGB White = new(1, 1, 1);

    /// <summary>
    /// Конвертирует линейный RGB в sRGB с гамма-коррекцией (2.2).
    /// Ограничивает значения диапазоном [0, 1] перед преобразованием.
    /// </summary>
    public Color ToColorSRGB()
    {
        // Ограничение значений для предотвращения артефактов
        double r = Clamp01(R);
        double g = Clamp01(G);
        double b = Clamp01(B);

        // Гамма-коррекция для компенсации нелинейности дисплеев
        r = Math.Pow(r, 1.0 / 2.2);
        g = Math.Pow(g, 1.0 / 2.2);
        b = Math.Pow(b, 1.0 / 2.2);

        return Color.FromArgb(
            (int)(255.999 * r),
            (int)(255.999 * g),
            (int)(255.999 * b)
        );
    }

    private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

    public override string ToString() => $"({R:0.###},{G:0.###},{B:0.###})";
}

/// <summary>
/// Запись о пересечении луча с объектом.
/// Содержит всю необходимую информацию для расчета освещения в точке пересечения.
/// </summary>
public readonly struct HitRecord
{
    public readonly double T;           // Параметр луча в точке пересечения
    public readonly Vec3 P;            // Точка пересечения в мировых координатах
    public readonly Vec3 Normal;       // Нормаль в точке пересечения (всегда направлена против луча)
    public readonly bool FrontFace;    // Указывает, пересек ли луч объект с внешней стороны
    public readonly Material Material; // Материал объекта в точке пересечения

    /// <summary>
    /// Создает запись о пересечении, автоматически определяя ориентацию нормали.
    /// Нормаль всегда направлена против луча для корректного расчета освещения.
    /// </summary>
    public HitRecord(double t, Vec3 p, Vec3 outwardNormal, Ray ray, Material material)
    {
        T = t;
        P = p;
        FrontFace = Vec3.Dot(ray.Direction, outwardNormal) < 0;  // Луч пришел с внешней стороны?
        Normal = FrontFace ? outwardNormal : -outwardNormal;     // Нормаль всегда против луча
        Material = material;
    }
}

/// <summary>
/// Интерфейс для всех объектов, которые могут пересекаться с лучом.
/// Является основой для иерархии геометрических объектов сцены.
/// </summary>
public interface IHittable
{
    bool Hit(in Ray ray, double tMin, double tMax, out HitRecord hit);
}

/// <summary>
/// Материал объекта, определяющий его оптические свойства.
/// Поддерживает диффузные, зеркальные и прозрачные материалы с различными параметрами.
/// </summary>
public sealed class Material
{
    public ColorRGB Albedo { get; set; } = new(1, 1, 1);  // Базовый цвет (диффузная компонента)

    // Зеркальные свойства
    public bool IsMirror { get; set; }                    // Флаг зеркального материала
    public double MirrorStrength { get; set; } = 0.85;    // Интенсивность зеркального отражения (0..1)

    // Свойства прозрачности/преломления
    public bool IsTransparent { get; set; }               // Флаг прозрачного материала
    public double Ior { get; set; } = 1.5;                // Коэффициент преломления (index of refraction)
    public double Transparency { get; set; } = 0.98;      // Прозрачность (0..1)
    public double ReflectionFactor { get; set; } = 0.1;   // Коэффициент отражения по Френелю

    // Свойства бликов (модель Фонга)
    public double PhongSpecular { get; set; } = 0.1;      // Интенсивность бликов
    public double PhongPower { get; set; } = 50.0;        // Жесткость бликов (экспонента)

    /// <summary>
    /// Создает глубокую копию материала.
    /// Используется для модификации базовых материалов без изменения оригинала.
    /// </summary>
    public Material Clone()
    {
        return new Material
        {
            Albedo = this.Albedo,
            IsMirror = this.IsMirror,
            MirrorStrength = this.MirrorStrength,
            IsTransparent = this.IsTransparent,
            Ior = this.Ior,
            Transparency = this.Transparency,
            ReflectionFactor = this.ReflectionFactor,
            PhongSpecular = this.PhongSpecular,
            PhongPower = this.PhongPower
        };
    }
}

/// <summary>
/// Точечный источник света с затуханием по закону обратных квадратов.
/// </summary>
public readonly struct PointLight
{
    public readonly Vec3 Position;     // Позиция источника в мировых координатах
    public readonly ColorRGB Color;    // Цвет излучения (белый или цветной)
    public readonly double Intensity;  // Интенсивность в условных единицах (ваттах)

    public PointLight(Vec3 position, ColorRGB color, double intensity)
    {
        Position = position;
        Color = color;
        Intensity = intensity;
    }
}

/// <summary>
/// Луч в трехмерном пространстве.
/// Определяется начальной точкой и направлением (не обязательно нормализованным).
/// </summary>
public readonly struct Ray
{
    public readonly Vec3 Origin;    // Начальная точка луча
    public readonly Vec3 Direction; // Направление луча

    public Ray(Vec3 origin, Vec3 direction)
    {
        Origin = origin;
        Direction = direction;
    }

    /// <summary>
    /// Вычисляет точку на луче на расстоянии t от начала.
    /// </summary>
    public Vec3 At(double t) => Origin + t * Direction;
}

/// <summary>
/// Перечисление для выбора зеркальной стены в Корнуэльской комнате.
/// Соответствует индексам в ComboBox интерфейса.
/// </summary>
public enum MirrorWall
{
    None = 0,
    Left = 1,
    Right = 2,
    Floor = 3,
    Ceiling = 4,
    Back = 5,
    Front = 6
}

/// <summary>
/// Перечисление для размещения второго источника света.
/// Определяет, на какой поверхности комнаты будет размещен дополнительный свет.
/// </summary>
public enum SecondLightPlacement
{
    None,
    Right,
    Left,
    Floor,
    Back,
    Front
}

/// <summary>
/// Параметры рендеринга, собираемые из пользовательского интерфейса.
/// Содержит все настройки для кастомизации процесса трассировки лучей.
/// </summary>
public sealed class RenderOptions
{
    public int Width { get; set; } = 1024;                    // Ширина выходного изображения
    public int Height { get; set; } = 768;                    // Высота выходного изображения
    public int SamplesPerPixel { get; set; } = 1;             // Количество лучей на пиксель (антиалиасинг)
    public int MaxDepth { get; set; } = 4;                    // Максимальная глубина рекурсии

    public bool MirrorSpheres { get; set; }                   // Делать левую сферу зеркальной
    public bool MirrorCubes { get; set; }                     // Делать левый куб зеркальным
    public bool TransparentSpheres { get; set; }              // Делать правую сферу прозрачной
    public bool TransparentCubes { get; set; }                // Делать правый куб прозрачным

    public MirrorWall MirrorWall { get; set; } = MirrorWall.None;  // Выбор зеркальной стены
    public SecondLightPlacement SecondLightPlacement { get; set; } = SecondLightPlacement.None;  // Второй источник света
}

/// <summary>
/// Генератор случайных чисел с потокобезопасностью (ThreadLocal).
/// Используется для случайного смещения лучей при антиалиасинге.
/// Каждый поток имеет свой экземпляр Random для предотвращения блокировок.
/// </summary>
internal static class Rng
{
    private static readonly ThreadLocal<Random> _rnd = new(() => new Random(Guid.NewGuid().GetHashCode()));
    public static double NextDouble() => _rnd.Value!.NextDouble();
}

/// <summary>
/// Трехмерный вектор с поддержкой основных математических операций.
/// Используется для представления точек, направлений, нормалей и цветов.
/// Оптимизирован для вычислений в трассировке лучей.
/// </summary>
public readonly struct Vec3
{
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

    // Базовые векторные операции
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double t) => new(a.X * t, a.Y * t, a.Z * t);
    public static Vec3 operator *(double t, Vec3 a) => a * t;
    public static Vec3 operator /(Vec3 a, double t) => new(a.X / t, a.Y / t, a.Z / t);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);

    public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared() => X * X + Y * Y + Z * Z;

    /// <summary>
    /// Возвращает нормализованную копию вектора.
    /// Защищено от деления на ноль.
    /// </summary>
    public Vec3 Normalized()
    {
        double len = Length();
        if (len <= 1e-12) return new Vec3(0, 0, 0);
        return this / len;
    }

    // Скалярное произведение (проекция)
    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    // Векторное произведение (перпендикуляр)
    public static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

    /// <summary>
    /// Вычисляет отраженный вектор по закону "угол падения равен углу отражения".
    /// </summary>
    /// <param name="v">Падающий вектор (должен быть нормализован)</param>
    /// <param name="n">Нормаль поверхности (должна быть нормализована)</param>
    public static Vec3 Reflect(Vec3 v, Vec3 n) => v - 2.0 * Dot(v, n) * n;

    /// <summary>
    /// Вычисляет преломленный вектор по закону Снеллиуса.
    /// Возвращает false при полном внутреннем отражении.
    /// </summary>
    /// <param name="uv">Падающий вектор (должен быть нормализован)</param>
    /// <param name="n">Нормаль поверхности (должна быть нормализована)</param>
    /// <param name="etaIOverEtaT">Отношение коэффициентов преломления (n1/n2)</param>
    /// <param name="refracted">Выходной параметр - преломленный вектор</param>
    public static bool Refract(Vec3 uv, Vec3 n, double etaIOverEtaT, out Vec3 refracted)
    {
        double cosTheta = Math.Min(Dot(-uv, n), 1.0);
        Vec3 rOutPerp = etaIOverEtaT * (uv + cosTheta * n);
        double k = 1.0 - rOutPerp.LengthSquared();

        if (k < 0)  // Полное внутреннее отражение
        {
            refracted = default;
            return false;
        }

        Vec3 rOutParallel = -Math.Sqrt(k) * n;
        refracted = rOutPerp + rOutParallel;
        return true;
    }

    public override string ToString() => $"({X}, {Y}, {Z})";
}