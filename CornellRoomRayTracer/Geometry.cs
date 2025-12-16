namespace CornwallRoom;

/// <summary>
/// Прямоугольник в плоскости XY
/// </summary>
public sealed class XYRect : IHittable
{
    private readonly double _x0, _x1, _y0, _y1, _k;
    private readonly bool _flipNormal;
    private readonly Material _material;

    public XYRect(double x0, double x1, double y0, double y1, double k, bool flipNormal, Material material)
    {
        _x0 = x0; _x1 = x1; _y0 = y0; _y1 = y1; _k = k;
        _flipNormal = flipNormal;
        _material = material;
    }

    /// <summary>
    /// Проверяет пересечение луча с прямоугольником в плоскости XY
    /// </summary>
    public bool Hit(in Ray ray, double tMin, double tMax, out HitRecord hit)
    {
        // Прямоугольник параллелен плоскости XY, луч должен иметь ненулевую Z-компоненту
        if (Math.Abs(ray.Direction.Z) < 1e-12) { hit = default; return false; }

        // Параметр пересечения с плоскостью z = k
        double t = (_k - ray.Origin.Z) / ray.Direction.Z;
        if (t < tMin || t > tMax) { hit = default; return false; }

        // Проверка, лежит ли точка пересечения в пределах прямоугольника
        double x = ray.Origin.X + t * ray.Direction.X;
        double y = ray.Origin.Y + t * ray.Direction.Y;
        if (x < _x0 || x > _x1 || y < _y0 || y > _y1) { hit = default; return false; }

        // Создание записи о пересечении
        Vec3 p = ray.At(t);
        Vec3 n = _flipNormal ? new Vec3(0, 0, -1) : new Vec3(0, 0, 1);  // Нормаль ±Z
        hit = new HitRecord(t, p, n, ray, _material);
        return true;
    }
}

/// <summary>
/// Прямоугольник в плоскости XZ
/// </summary>
public sealed class XZRect : IHittable
{
    private readonly double _x0, _x1, _z0, _z1, _k;
    private readonly bool _flipNormal;
    private readonly Material _material;

    public XZRect(double x0, double x1, double z0, double z1, double k, bool flipNormal, Material material)
    {
        _x0 = x0; _x1 = x1; _z0 = z0; _z1 = z1; _k = k;
        _flipNormal = flipNormal;
        _material = material;
    }

    /// <summary>
    /// Проверяет пересечение луча с прямоугольником в плоскости XZ
    /// </summary>
    public bool Hit(in Ray ray, double tMin, double tMax, out HitRecord hit)
    {
        if (Math.Abs(ray.Direction.Y) < 1e-12) { hit = default; return false; }
        double t = (_k - ray.Origin.Y) / ray.Direction.Y;
        if (t < tMin || t > tMax) { hit = default; return false; }

        double x = ray.Origin.X + t * ray.Direction.X;
        double z = ray.Origin.Z + t * ray.Direction.Z;
        if (x < _x0 || x > _x1 || z < _z0 || z > _z1) { hit = default; return false; }

        Vec3 p = ray.At(t);
        Vec3 n = _flipNormal ? new Vec3(0, -1, 0) : new Vec3(0, 1, 0);  // Нормаль ±Y
        hit = new HitRecord(t, p, n, ray, _material);
        return true;
    }
}

/// <summary>
/// Прямоугольник в плоскости YZ
/// </summary>
public sealed class YZRect : IHittable
{
    private readonly double _y0, _y1, _z0, _z1, _k;
    private readonly bool _flipNormal;
    private readonly Material _material;

    public YZRect(double y0, double y1, double z0, double z1, double k, bool flipNormal, Material material)
    {
        _y0 = y0; _y1 = y1; _z0 = z0; _z1 = z1; _k = k;
        _flipNormal = flipNormal;
        _material = material;
    }

    /// <summary>
    /// Проверяет пересечение луча с прямоугольником в плоскости YZ
    /// </summary>
    public bool Hit(in Ray ray, double tMin, double tMax, out HitRecord hit)
    {
        if (Math.Abs(ray.Direction.X) < 1e-12) { hit = default; return false; }
        double t = (_k - ray.Origin.X) / ray.Direction.X;
        if (t < tMin || t > tMax) { hit = default; return false; }

        double y = ray.Origin.Y + t * ray.Direction.Y;
        double z = ray.Origin.Z + t * ray.Direction.Z;
        if (y < _y0 || y > _y1 || z < _z0 || z > _z1) { hit = default; return false; }

        Vec3 p = ray.At(t);
        Vec3 n = _flipNormal ? new Vec3(-1, 0, 0) : new Vec3(1, 0, 0);  // Нормаль ±X
        hit = new HitRecord(t, p, n, ray, _material);
        return true;
    }
}

public sealed class Sphere : IHittable
{
    public Vec3 Center { get; }
    public double Radius { get; }
    public Material Material { get; }

    public Sphere(Vec3 center, double radius, Material material)
    {
        Center = center;
        Radius = radius;
        Material = material;
    }

    /// <summary>
    /// Проверяет пересечение луча со сферой
    /// </summary>
    public bool Hit(in Ray ray, double tMin, double tMax, out HitRecord hit)
    {
        // Вектор от центра сферы к началу луча
        Vec3 oc = ray.Origin - Center;

        // Коэффициенты квадратного уравнения (o + t*d - c)^2 = r^2
        double a = ray.Direction.LengthSqr();
        double halfB = Vec3.Dot(oc, ray.Direction);  // Упрощенное b/2
        double c = oc.LengthSqr() - Radius * Radius;

        // Дискриминант (упрощенный: (b/2)^2 - ac)
        double discriminant = halfB * halfB - a * c;

        if (discriminant < 0)  // Нет действительных корней - нет пересечения
        {
            hit = default;
            return false;
        }

        double sqrtD = Math.Sqrt(discriminant);

        // Ближайший корень
        double root = (-halfB - sqrtD) / a;
        if (root < tMin || root > tMax)
        {
            root = (-halfB + sqrtD) / a;  // Дальний корень
            if (root < tMin || root > tMax)
            {
                hit = default;
                return false;
            }
        }

        // Вычисление точки пересечения и нормали
        Vec3 p = ray.At(root);
        Vec3 outward = (p - Center) / Radius;  // Нормализованная нормаль
        hit = new HitRecord(root, p, outward, ray, Material);
        return true;
    }
}