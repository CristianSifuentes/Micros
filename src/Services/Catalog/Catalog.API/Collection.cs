namespace Catalog.API;

/// <summary>The opening collection. Every creator and house here is fictional.</summary>
public static class Collection
{
    public static IEnumerable<Piece> Opening() =>
    [
        Art("0190a000-0000-7000-8000-000000000001", "Nocturne en bleu de Prusse", "Inés Valcárcel",
            "Óleo y cera fría sobre lino", 2019, "180 × 140 cm", 40_000, 60_000,
            "Valcárcel pinta de noche y raspa de día. Bajo el azul hay siete cuadros anteriores; el último es el único que decidió no borrar."),
        Art("0190a000-0000-7000-8000-000000000002", "Estudio para un jardín que no existe", "Tomás Arregui",
            "Gouache y grafito sobre papel", 2021, "56 × 76 cm", 8_000, 12_000,
            "Parte de una serie de cuarenta jardines imposibles: la botánica es exacta, la geografía no."),
        Art("0190a000-0000-7000-8000-000000000003", "Kintsugi n.º 7", "Aiko Murasaki",
            "Gres, laca urushi y oro", 2020, "34 × 28 × 28 cm", 15_000, 20_000,
            "La vasija se rompió en el horno. Murasaki tardó un año en decidir que la grieta era la obra."),
        Art("0190a000-0000-7000-8000-000000000004", "La hora violeta", "Lucía Montenegro",
            "Temple al huevo sobre tabla", 2023, "90 × 120 cm", 22_000, 30_000,
            "Pintado con la técnica de los primitivos italianos: veinte veladuras para un crepúsculo que dura once minutos."),
        Wear("0190a000-0000-7000-8000-000000000005", "Abrigo Columna", "Maison Solène",
            "Cachemir doble faz, cosido a mano", 2025, "Otoño–Invierno 2025", 4_200,
            "Sin forro, sin entretela: dos telas unidas por una costura invisible que lleva catorce horas de aguja."),
        Wear("0190a000-0000-7000-8000-000000000006", "Vestido Ménades", "Atelier Ourania",
            "Seda plisada a mano", 2026, "Primavera–Verano 2026", 6_800,
            "Ochocientos pliegues fijados con calor y paciencia. Se guarda enrollado, como un pergamino."),
        Wear("0190a000-0000-7000-8000-000000000007", "Bolso Arquitecto", "Casa Veladura",
            "Becerro de curtición vegetal", 2025, "Edición permanente", 2_350,
            "Una sola pieza de cuero doblada cuatro veces. Envejece hacia el color del coñac."),
        Object("0190a000-0000-7000-8000-000000000008", "Silla Umbra", "Estudio Paloma Reyes",
            "Roble carbonizado (yakisugi)", 2024, "Edición de 12", null, null, 9_500,
            "La madera se quema para protegerla. Lo que queda es negro, sedoso y huele a invierno durante un año."),
        Object("0190a000-0000-7000-8000-000000000009", "Lámpara Halo", "Bruno Castell",
            "Vidrio opalino soplado y latón", 2022, "Ø 42 cm", 6_000, 9_000, null,
            "Soplada en un solo gesto. La luz no sale de la lámpara: se queda en ella."),
    ];

    private static Piece Art(string id, string title, string creator, string medium, int year, string dimensions,
        decimal low, decimal high, string story) => new()
    {
        Id = Guid.Parse(id), Title = title, Creator = creator, Discipline = Discipline.Art, Medium = medium,
        Year = year, Dimensions = dimensions, EstimateLow = low, EstimateHigh = high, Story = story,
    };

    private static Piece Wear(string id, string title, string creator, string medium, int year, string season,
        decimal price, string story) => new()
    {
        Id = Guid.Parse(id), Title = title, Creator = creator, Discipline = Discipline.Fashion, Medium = medium,
        Year = year, Dimensions = season, Price = price, Story = story,
    };

    private static Piece Object(string id, string title, string creator, string medium, int year, string dimensions,
        decimal? low, decimal? high, decimal? price, string story) => new()
    {
        Id = Guid.Parse(id), Title = title, Creator = creator, Discipline = Discipline.Design, Medium = medium,
        Year = year, Dimensions = dimensions, EstimateLow = low, EstimateHigh = high, Price = price, Story = story,
    };
}
