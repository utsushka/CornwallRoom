using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CornwallRoom;

/// <summary>
/// Создает геометрию комнаты, объекты, материалы и освещение в соответствии с параметрами рендеринга
/// </summary>
public static class SceneFactory
{
    // Размеры комнаты
    private const double X0 = -1, X1 = 1;
    private const double Y0 = 0, Y1 = 2;
    private const double Z0 = -1, Z1 = 1;

    /// <summary>
    /// Создает сцену с заданными параметрами
    /// </summary>
    public static IHittable BuildCornwallRoom(RenderOptions opts, out Camera camera, out PointLight[] lights)
    {
        var world = new HittableList();

        // Базовые материалы для стен комнаты
        var whiteWall = new Material
        {
            Albedo = new ColorRGB(0.8, 0.8, 0.8), 
            PhongSpecular = 0.08,  // Матовая поверхность
            PhongPower = 40
        };

        var redWall = new Material
        {
            Albedo = new ColorRGB(0.85, 0.2, 0.2),  
            PhongSpecular = 0.05,
            PhongPower = 40
        };

        var greenWall = new Material
        {
            Albedo = new ColorRGB(0.2, 0.85, 0.2), 
            PhongSpecular = 0.05,
            PhongPower = 40
        };

        var blueWall = new Material
        {
            Albedo = new ColorRGB(0.2, 0.2, 0.85), 
            PhongSpecular = 0.05,
            PhongPower = 40
        };

        // Базовый материал для сфер
        var sphereBase = new Material
        {
            Albedo = new ColorRGB(0.75, 0.75, 0.75),
            PhongSpecular = 0.12, 
            PhongPower = 80
        };

        // Базовый материал для кубов
        var cubeBase = new Material
        {
            Albedo = new ColorRGB(0.75, 0.75, 0.2),
            PhongSpecular = 0.10,
            PhongPower = 60
        };

        /// <summary>
        /// Локальная функция для создания зеркальной стены
        /// </summary>
        Material Wall(Material baseMat, MirrorWall which)
        {
            if (opts.MirrorWall != which) return baseMat;

            // Клонируем материал и делаем его зеркальным
            var mirrorWall = baseMat.Clone();
            mirrorWall.IsMirror = true;
            mirrorWall.MirrorStrength = 0.92; 
            mirrorWall.PhongSpecular = 0.0;   
            mirrorWall.PhongPower = 1;
            return mirrorWall;
        }

        // Построение стен комнаты
        world.Add(new YZRect(Y0, Y1, Z0, Z1, X0, flipNormal: false, material: Wall(redWall, MirrorWall.Left)));
        world.Add(new YZRect(Y0, Y1, Z0, Z1, X1, flipNormal: true, material: Wall(greenWall, MirrorWall.Right)));
        world.Add(new XZRect(X0, X1, Z0, Z1, Y0, flipNormal: false, material: Wall(whiteWall, MirrorWall.Floor)));
        world.Add(new XZRect(X0, X1, Z0, Z1, Y1, flipNormal: true, material: Wall(whiteWall, MirrorWall.Ceiling)));
        world.Add(new XYRect(X0, X1, Y0, Y1, Z0, flipNormal: false, material: Wall(blueWall, MirrorWall.Back)));
        world.Add(new XYRect(X0, X1, Y0, Y1, Z1, flipNormal: true, material: Wall(whiteWall, MirrorWall.Front)));

        // Создание материалов для сфер с применением настроек пользователя
        var sphere1Mat = sphereBase.Clone();  // Левая сфера 
        var sphere2Mat = sphereBase.Clone();  // Правая сфера

        if (opts.MirrorSpheres)
        {
            sphere1Mat.IsMirror = true;
            sphere1Mat.MirrorStrength = 0.8;
            sphere1Mat.Reflection = 1.0;  // Полное отражение
        }

        if (opts.TransparentSpheres)
        {
            sphere2Mat.IsTransparent = true;
            sphere2Mat.Refraction = 1.1;  // Коэффициент преломления чуть выше воздуха
            sphere2Mat.Transparency = 0.95;  
            sphere2Mat.Reflection = 0.1;  // 10% отражения по Френелю
            sphere2Mat.Albedo = new ColorRGB(0.9, 0.9, 0.9);  
        }

        // Размещение сфер в комнате
        world.Add(new Sphere(new Vec3(-0.45, 0.35, -0.15), 0.35, sphere1Mat)); 
        world.Add(new Sphere(new Vec3(0.45, 0.30, 0.25), 0.30, sphere2Mat)); 

        // Создание материалов для кубов
        var cube1Mat = cubeBase.Clone();  // Левый куб 
        var cube2Mat = cubeBase.Clone();  // Правый куб

        if (opts.MirrorCubes)
        {
            cube1Mat.IsMirror = true;
            cube1Mat.MirrorStrength = 0.8;
            cube1Mat.Reflection = 1.0;
        }

        if (opts.TransparentCubes)
        {
            cube2Mat.IsTransparent = true;
            cube2Mat.Refraction = 1.1;
            cube2Mat.Transparency = 0.95;
            cube2Mat.Reflection = 0.1;
            cube2Mat.Albedo = new ColorRGB(0.9, 0.9, 0.9);
        }

        // Размещение кубов в комнате
        AddCube(world, new Vec3(-0.85, 0.0, -0.85), new Vec3(-0.35, 0.90, -0.35), cube1Mat);  
        AddCube(world, new Vec3(0.10, 0.0, -0.75), new Vec3(0.65, 0.60, -0.20), cube2Mat);   

        // Основной источник света
        var light1 = new PointLight(new Vec3(0.0, 1.85, 0.0), new ColorRGB(1, 1, 1), intensity: 20.0);

        // Добавление второго источника света если выбран
        if (opts.SecondLightPlacement == SecondLightPlacement.None) { lights = new[] { light1 }; }
        else
        {
            Vec3 pos2;
            switch (opts.SecondLightPlacement)
            {
                case SecondLightPlacement.Floor: pos2 = new Vec3(0.0, Y0 + 0.05, 0.0); break;
                case SecondLightPlacement.Right: pos2 = new Vec3(X1 - 0.05, (Y0 + Y1) * 0.5, 0.0); break;
                case SecondLightPlacement.Left: pos2 = new Vec3(X0 + 0.05, (Y0 + Y1) * 0.5, 0.0); break;
                case SecondLightPlacement.Back: pos2 = new Vec3(0.0, (Y0 + Y1) * 0.5, Z0 + 0.05); break;
                case SecondLightPlacement.Front: pos2 = new Vec3(0.0, (Y0 + Y1) * 0.5, Z1 - 0.05); break;
                default: pos2 = new Vec3(0.0, (Y0 + Y1) * 0.5, Z0 + 0.05); break;
            }

            var light2 = new PointLight(pos2, new ColorRGB(1, 0.98, 0.95), intensity: 10.0);
            lights = new[] { light1, light2 };
        }

        // Настройка камеры
        double aspect = (double)opts.Width / opts.Height;
        camera = new Camera(
            lookFrom: new Vec3(0.0, 0.8, 0.95),    
            lookAt: new Vec3(0.0, 1.0, -0.7),     
            vUp: new Vec3(0, 1, 0),                
            vfovDeg: 70,                        
            aspect: aspect
        );

        return world;
    }

    /// <summary>
    /// Создает полый куб из 6 прямоугольных граней
    /// </summary>
    private static void AddCube(HittableList world, Vec3 min, Vec3 max, Material material)
    {
        double x0 = min.X, x1 = max.X;
        double y0 = min.Y, y1 = max.Y;
        double z0 = min.Z, z1 = max.Z;

        world.Add(new YZRect(y0, y1, z0, z1, x0, flipNormal: true, material: material));    // Левая грань
        world.Add(new YZRect(y0, y1, z0, z1, x1, flipNormal: false, material: material));   // Правая грань
        world.Add(new XZRect(x0, x1, z0, z1, y0, flipNormal: true, material: material));    // Нижняя грань
        world.Add(new XZRect(x0, x1, z0, z1, y1, flipNormal: false, material: material));   // Верхняя грань
        world.Add(new XYRect(x0, x1, y0, y1, z0, flipNormal: true, material: material));    // Задняя грань
        world.Add(new XYRect(x0, x1, y0, y1, z1, flipNormal: false, material: material));   // Передняя грань
    }
}

/// <summary>
/// Основной класс трассировщика лучей.
/// Выполняет рендеринг сцены в растровое изображение с использованием многопоточности
/// </summary>
public static class RayTracer
{
    /// <summary>
    /// Рендер сцены
    /// </summary>
    public static Bitmap RenderCornwall(RenderOptions opts, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Построение сцены с текущими параметрами
        var scene = SceneFactory.BuildCornwallRoom(opts, out var camera, out var lights);

        int w = opts.Width;
        int h = opts.Height;
        int spp = 5;  // Антиалиасинг: несколько лучей на пиксель
        int maxDepth = 5;    // Максимальная глубина рекурсии для отражений/преломлений

        // Создание целевого растрового изображения
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

        int stride = data.Stride;
        int bytes = stride * h;
        byte[] buffer = new byte[bytes];

        // Параллельный рендеринг строк изображения
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            int rowIndex = y * stride;
            double v = 1.0 - (y + 0.5) / h;  // Нормализованная координата Y (0..1)

            for (int x = 0; x < w; x++)
            {
                ColorRGB col = ColorRGB.Black;

                // Многолучевое сэмплирование для антиалиасинга
                for (int s = 0; s < spp; s++)
                {
                    // Случайное смещение внутри пикселя (кроме случая spp=1)
                    double u = (x + (spp == 1 ? 0.5 : Rng.NextDouble())) / w;
                    double vv = 1.0 - (y + (spp == 1 ? 0.5 : Rng.NextDouble())) / h;

                    var ray = camera.GetRay(u, vv);
                    col += TraceRay(ray, scene, lights, maxDepth, 1.0);  // Коэффициент преломления воздуха = 1.0
                }

                // Усреднение цвета по количеству сэмплов
                col = col * (1.0 / spp);
                var c = col.ToColorSRGB(); 

                // Запись пикселя в буфер
                int i = rowIndex + x * 3;
                buffer[i + 0] = c.B;
                buffer[i + 1] = c.G;
                buffer[i + 2] = c.R;
            }
        });

        // Копирование буфера в растровое изображение
        Marshal.Copy(buffer, 0, data.Scan0, bytes);
        bmp.UnlockBits(data);
        return bmp;
    }

    /// <summary>
    /// Трассирует луч через сцену с рекурсивным учетом отражений и преломлений.
    /// Вычисляет цвет пикселя на основе пересечений с объектами и освещения.
    /// </summary>
    private static ColorRGB TraceRay(in Ray ray, IHittable world, PointLight[] lights, int depth, double environmentIor)
    {
        if (depth <= 0) return ColorRGB.Black;

        // Поиск ближайшего пересечения луча со сценой
        if (!world.Hit(ray, 1e-4, 1e30, out var hit)) return ColorRGB.Black;

        var mat = hit.Material;
        var p = hit.P;
        var n = hit.Normal;

        // Обработка зеркальных материалов
        if (mat.IsMirror)
        {
            Vec3 reflDir = Vec3.Reflect(ray.Direction, n).Normalized(); // угол падения = угол отражения
            var reflRay = new Ray(p + reflDir * 1e-4, reflDir);
            var reflColor = TraceRay(reflRay, world, lights, depth - 1, environmentIor);
            return reflColor * mat.MirrorStrength;  // Учет коэффициента отражения материала
        }

        // Обработка прозрачных материалов (преломление + отражение по Френелю)
        if (mat.IsTransparent)
        {
            bool exiting = !hit.FrontFace;  // Определяем, выходит ли луч из материала

            // Коэффициент преломления: n1/n2 или n2/n1 в зависимости от направления
            double refractionRatio = exiting ? mat.Refraction / environmentIor : environmentIor / mat.Refraction;

            Vec3 unitDirection = ray.Direction.Normalized();

            // Вычисление преломленного луча
            Vec3 refractedDir;
            bool canRefract = Vec3.Refract(unitDirection, n, refractionRatio, out refractedDir);
            ColorRGB refractedColor = ColorRGB.Black;

            if (canRefract)
            {
                var refractedRay = new Ray(p + refractedDir * 1e-3, refractedDir);
                refractedColor = TraceRay(refractedRay, world, lights, depth - 1,
                                          exiting ? environmentIor : mat.Refraction);
                refractedColor = refractedColor * mat.Transparency;  // Учет прозрачности материала
            }

            // Вычисление отраженного луча (отражение по Френелю)
            Vec3 reflectedDir = Vec3.Reflect(unitDirection, n).Normalized();
            var reflectedRay = new Ray(p + reflectedDir * 1e-3, reflectedDir);
            ColorRGB reflectedColor = TraceRay(reflectedRay, world, lights, depth - 1, environmentIor);

            // Смешивание отраженной и преломленной компонент
            double kr = mat.Reflection;  // Коэффициент Френеля (доля отражения)
            ColorRGB surface = reflectedColor * kr + refractedColor * (1.0 - kr);

            // Умножение на цвет материала (подкрашивание стекла)
            surface *= mat.Albedo;

            return surface;
        }

        // Ambient (фоновое освещение) - минимальная подсветка даже в тени
        ColorRGB local = mat.Albedo * 0.02;  

        Vec3 viewDir = (-ray.Direction).Normalized();

        // Учет всех источников света
        foreach (var light in lights)
        {
            Vec3 toL = light.Position - p;
            double dist2 = toL.LengthSqr();
            double dist = Math.Sqrt(dist2);
            Vec3 ldir = toL / dist;

            // Теневой луч: проверка видимости источника
            var shadowRay = new Ray(p + n * 1e-3, ldir);
            if (world.Hit(shadowRay, 1e-4, dist - 1e-4, out _)) continue;  // Объект в тени

            // Диффузная компонента (закон косинусов Ламберта)
            double ndotl = Math.Max(0.0, Vec3.Dot(n, ldir)); // cos(угла) [0..1]
            double atten = light.Intensity / (4.0 * Math.PI * dist2);  // Затухание

            local += mat.Albedo * light.Color * (ndotl * atten);

            // Зеркальная компонента (модель Фонга)
            if (mat.PhongSpecular > 0)
            {
                Vec3 h = (ldir + viewDir).Normalized();  // Вектор полупути
                double ndoth = Math.Max(0.0, Vec3.Dot(n, h));

                // Формула Фонга: specular * (cos)^power
                double spec = mat.PhongSpecular * Math.Pow(ndoth, mat.PhongPower);
                local += light.Color * (spec * atten);
            }
        }

        return local;
    }
}