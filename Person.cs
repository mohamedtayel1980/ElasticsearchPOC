using Nest;

public class Person
{
    [Text(Name = "full_name_ar")]
    public string FullName { get; set; }

    [Text(Name = "full_name_en")]
    public string FullNameEn { get; set; }
}
