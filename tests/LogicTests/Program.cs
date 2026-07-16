using RaceFilter.Model;

const int ModelType = 2;
const int Tribe = 4;
const int FaceType = 5;
const int HairStyle = 6;

static void Equal(byte expected, byte actual, string field)
{
    if (expected != actual)
        throw new InvalidOperationException($"{field}: expected {expected}, actual {actual}");
}

var customize = new byte[32];
customize[Tribe] = 1;
customize[FaceType] = 1;
customize[ModelType] = 1;
customize[HairStyle] = 1;
RaceRemap.Apply(customize, RaceId.Hyur, null);
Equal(1, customize[FaceType], "face preserves lower bound");
Equal(1, customize[ModelType], "model preserves lower bound");
Equal(1, customize[HairStyle], "hair preserves lower bound");

customize[FaceType] = 0;
customize[ModelType] = 0;
customize[HairStyle] = 0;
RaceRemap.Apply(customize, RaceId.Hrothgar, null);
Equal(1, customize[FaceType], "face maps zero safely");
Equal(1, customize[ModelType], "model maps zero safely");
Equal(1, customize[HairStyle], "hair maps zero safely");

Console.WriteLine("RaceRemap logic tests passed.");
