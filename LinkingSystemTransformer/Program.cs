using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using DoenaSoft.DVDProfiler.DVDProfilerHelper;
using DoenaSoft.DVDProfiler.DVDProfilerXML;
using DoenaSoft.DVDProfiler.DVDProfilerXML.Version400;

namespace LinkingSystemTransformer
{
    public static class Program
    {
        private static WindowHandle s_WindowHandle = new WindowHandle();

        internal static Int32 s_IdCounter = 1;

        private static List<String> KnownFirstnamePrefixes;

        private static List<String> KnownLastnamePrefixes;

        private static List<String> KnownLastnameSuffixes;

        private static List<String> StageNames;

        [STAThread()]
        public static void Main()
        {
            Console.WriteLine("Welcome to the DVDProfiler Actor Linking Transformation Simulation!");
            Console.WriteLine("Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine();
            Console.WriteLine("Please select a \"collection.xml\" and a target location for the output files!");
            Console.WriteLine("(You should see a file dialog. If not, please minimize your other programs.)");

            try
            {
                #region Phase 1: Ask For File Locations

                String collectionFile;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Collection.xml|*.xml";
                    ofd.CheckFileExists = true;
                    ofd.Multiselect = false;
                    ofd.Title = "Select Source File";
                    ofd.RestoreDirectory = true;

                    if (ofd.ShowDialog(s_WindowHandle) == DialogResult.Cancel)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Aborted.");

                        return;
                    }

                    collectionFile = ofd.FileName;
                }

                String targetFolder;
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Target Folder";

                    if (fbd.ShowDialog(s_WindowHandle) == DialogResult.Cancel)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Aborted.");

                        return;
                    }

                    targetFolder = fbd.SelectedPath;
                }

                #endregion

                #region Phase 2: Read XML

                Console.WriteLine();
                Console.WriteLine("Tranforming data:");

                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Collection));

                Collection collection;
                using (FileStream fs = new FileStream(collectionFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    collection = (Collection)(xmlSerializer.Deserialize(fs));
                }

                #endregion

                if (collection.DVDList?.Length > 0)
                {
                    #region Phase 3: Create new Online Actor Database

                    PersonHashtable castAndCrewHash = new PersonHashtable(collection.DVDList.Length * 50);

                    foreach (DVD dvd in collection.DVDList)
                    {
                        if (dvd.CastList != null && dvd.CastList.Length > 0)
                        {
                            foreach (Object possibleCast in dvd.CastList)
                            {
                                FillDynamicHash<CastMember>(castAndCrewHash, possibleCast);
                            }
                        }

                        if (dvd.CrewList != null && dvd.CrewList.Length > 0)
                        {
                            foreach (Object possibleCrew in dvd.CrewList)
                            {
                                FillDynamicHash<CrewMember>(castAndCrewHash, possibleCrew);
                            }
                        }
                    }

                    using (FileStream fs = new FileStream(targetFolder + "\\OnlineActorDatabase.txt", FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (StreamWriter sw = new StreamWriter(fs, Encoding.GetEncoding(1252)))
                        {
                            sw.Write("ID".PadRight(7));
                            sw.Write("First Name".PadRight(20));
                            sw.Write("Middle Name".PadRight(20));
                            sw.Write("Last Name".PadRight(20));
                            sw.WriteLine("Birth Year");
                            sw.WriteLine("".PadRight(77, '-'));

                            foreach (KeyValuePair<PersonKey, Int32> kvp in castAndCrewHash)
                            {
                                sw.Write(kvp.Value.ToString().PadLeft(6, '0').PadRight(7));
                                sw.Write(kvp.Key.Person.FirstName.PadRight(20));
                                sw.Write(kvp.Key.Person.MiddleName.PadRight(20));
                                sw.Write(kvp.Key.Person.LastName.PadRight(20));

                                if (kvp.Key.Person.BirthYear != 0)
                                {
                                    sw.WriteLine(kvp.Key.Person.BirthYear);
                                }
                                else
                                {
                                    sw.WriteLine();
                                }
                            }
                        }
                    }

                    #endregion

                    #region Phase 4: Create Profile Files

                    KnownFirstnamePrefixes = InitList(@"Data\KnownFirstnamePrefixes.txt");

                    KnownLastnamePrefixes = InitList(@"Data\KnownLastnamePrefixes.txt");

                    KnownLastnameSuffixes = InitList(@"Data\KnownLastnameSuffixes.txt");

                    StageNames = InitList(@"Data\StageNames.txt");

                    foreach (DVD dvd in collection.DVDList)
                    {
                        using (FileStream fs = new FileStream(targetFolder + "\\" + dvd.ID + "_Cast.txt", FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (StreamWriter sw = new StreamWriter(fs, Encoding.GetEncoding(1252)))
                            {
                                sw.WriteLine(dvd.Title);
                                sw.WriteLine();
                                sw.Write("ID".PadRight(7));
                                sw.Write("First Name".PadRight(20));
                                sw.Write("Middle Name".PadRight(20));
                                sw.Write("Last Name".PadRight(20));
                                sw.WriteLine("Role".PadRight(20));
                                sw.WriteLine("".PadRight(87, '-'));

                                if (dvd.CastList?.Length > 0)
                                {
                                    foreach (Object possibleCast in dvd.CastList)
                                    {
                                        CastMember cast = possibleCast as CastMember;

                                        if (cast != null)
                                        {
                                            sw.Write(castAndCrewHash[cast].ToString().PadLeft(6, '0').PadRight(7));

                                            if (String.IsNullOrEmpty(cast.CreditedAs))
                                            {
                                                sw.Write(cast.FirstName.PadRight(20));
                                                sw.Write(cast.MiddleName.PadRight(20));
                                                sw.Write(cast.LastName.PadRight(20));
                                                sw.WriteLine(cast.Role);
                                            }
                                            else
                                            {
                                                Name name = ParsePersonName(cast.CreditedAs);

                                                sw.Write(name.FirstName.ToString().PadRight(20));
                                                sw.Write(name.MiddleName.ToString().PadRight(20));
                                                sw.Write(name.LastName.ToString().PadRight(20));
                                                sw.WriteLine(cast.Role);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error: {0}", ex.Message);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Press <Enter> to exit.");

                Console.ReadLine();
            }
        }

        private static Name ParsePersonName(String fullname)
        {
            Name retVal = new Name();

            if (StageNames.Contains(fullname.ToLower()))
            {
                retVal.FirstName = new StringBuilder(fullname);

                return (retVal);
            }

            String[] nameSplit = fullname.Split('.');

            StringBuilder dotSplitter = new StringBuilder();

            for (Int32 i = 0; i < nameSplit.Length - 1; i++)
            {
                dotSplitter.Append(nameSplit[i].Trim() + ". ");
            }

            dotSplitter.Append(nameSplit[nameSplit.Length - 1].Trim());

            fullname = dotSplitter.ToString().Trim();
            fullname = CheckForQuotes(fullname, '\'', 0);
            fullname = CheckForQuotes(fullname, '"', 0);

            nameSplit = fullname.Split(' ');

            if (nameSplit.Length > 0)
            {
                nameSplit[0] = nameSplit[0].Replace("#SpacePlaceHolder#", " ");
            }

            if (nameSplit.Length == 1)
            {
                retVal.FirstName = new StringBuilder(nameSplit[0]);

                return (retVal);
            }

            Int32 beginOfMiddleName = -1;

            Int32 beginOfLastName = -1;

            Boolean canBeSuffix = true;

            Boolean canBePrefix = false;

            for (Int32 i = nameSplit.Length - 1; i >= 1; i--)
            {
                nameSplit[i] = nameSplit[i].Replace("#SpacePlaceHolder#", " ");

                if (canBeSuffix)
                {
                    beginOfLastName = i;

                    if (KnownLastnameSuffixes.Contains(nameSplit[i].ToLower()) == false)
                    {
                        canBeSuffix = false;

                        canBePrefix = true;
                    }

                    continue;
                }

                if (canBePrefix)
                {
                    if (KnownLastnamePrefixes.Contains(nameSplit[i].ToLower()))
                    {
                        beginOfLastName = i;

                        continue;
                    }
                }

                if ((i > 0) && (beginOfLastName > 1))
                {
                    beginOfMiddleName = 1;
                }
            }

            if (KnownFirstnamePrefixes.Contains(nameSplit[0].ToLower()))
            {
                for (Int32 i = 1; i < nameSplit.Length; i++)
                {
                    if (beginOfMiddleName == i)
                    {
                        beginOfMiddleName++;
                    }

                    if (beginOfLastName == i)
                    {
                        beginOfLastName++;
                    }

                    if (KnownFirstnamePrefixes.Contains(nameSplit[i].ToLower()))
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (beginOfMiddleName == beginOfLastName)
            {
                beginOfMiddleName = -1;
            }

            if (beginOfMiddleName > 0)
            {
                for (Int32 i = 0; i < beginOfMiddleName; i++)
                {
                    retVal.FirstName.Append(" " + nameSplit[i]);
                }

                retVal.FirstName = new StringBuilder(retVal.FirstName.ToString().Trim());

                for (Int32 i = beginOfMiddleName; i < beginOfLastName; i++)
                {
                    retVal.MiddleName.Append(" " + nameSplit[i]);
                }

                retVal.MiddleName = new StringBuilder(retVal.MiddleName.ToString().Trim());
            }
            else
            {
                for (Int32 i = 0; i < beginOfLastName; i++)
                {
                    retVal.FirstName.Append(" " + nameSplit[i]);
                }

                retVal.FirstName = new StringBuilder(retVal.FirstName.ToString().Trim());
            }

            for (Int32 i = beginOfLastName; i < nameSplit.Length; i++)
            {
                retVal.LastName.Append(" " + nameSplit[i]);
            }

            retVal.LastName = new StringBuilder(retVal.LastName.ToString().Trim());

            return (retVal);
        }

        private static String CheckForQuotes(String fullname
            , Char unsplittable
            , Int32 rootIndexOf)
        {
            if (rootIndexOf < fullname.Length)
            {
                Int32 indexOf = fullname.IndexOf(unsplittable, rootIndexOf);

                if ((indexOf != -1) && (indexOf != fullname.Length - 1))
                {
                    Int32 indexOf2 = fullname.IndexOf(unsplittable, indexOf + 1);

                    if (indexOf2 != -1)
                    {
                        StringBuilder newName = new StringBuilder();

                        if (indexOf != 0)
                        {
                            newName.Append(fullname.Substring(0, indexOf));
                        }

                        String section = fullname.Substring(indexOf, indexOf2 - indexOf + 1);

                        newName.Append(section.Replace(" ", "#SpacePlaceHolder#"));

                        if (indexOf2 != fullname.Length - 1)
                        {
                            newName.Append(fullname.Substring(indexOf2 + 1, fullname.Length - indexOf2 - 1));
                        }

                        fullname = newName.ToString();

                        indexOf2 = fullname.IndexOf(unsplittable, indexOf + 1);

                        return (CheckForQuotes(fullname, unsplittable, indexOf2 + 1));
                    }
                }
            }

            return (fullname);
        }

        private static void FillDynamicHash<T>(PersonHashtable personHash
            , Object possiblePerson)
            where T : class, IPerson
        {
            T person = possiblePerson as T;

            if (person != null)
            {
                if (personHash.ContainsKey(person) == false)
                {
                    personHash.Add(person);
                }
            }
        }

        private static List<String> InitList(String fileName)
        {
            List<String> list = new List<String>();

            if (File.Exists(fileName))
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    while (sr.EndOfStream == false)
                    {
                        String line = sr.ReadLine();

                        if (String.IsNullOrEmpty(line) == false)
                        {
                            list.Add(line.ToLower());
                        }
                    }
                }
            }

            return (list);
        }
    }

    internal class PersonHashtable : Hashtable<PersonKey>
    {
        internal PersonHashtable(Int32 capacity)
            : base(capacity)
        { }

        internal void Add(IPerson person)
        {
            Add(new PersonKey(person));
        }

        internal Boolean ContainsKey(IPerson person)
            => (ContainsKey(new PersonKey(person)));

        internal Int32 this[IPerson person]
            => (base[new PersonKey(person)]);
    }

    internal class Hashtable<TKey> : Dictionary<TKey, Int32>
    {
        internal Hashtable(Int32 capacity)
            : base(capacity)
        { }

        internal void Add(TKey key)
        {
            Add(key, Program.s_IdCounter++);
        }
    }

    internal class PersonKey
    {
        private IPerson m_Person;

        internal IPerson Person
            => (m_Person);

        internal PersonKey(IPerson person)
        {
            m_Person = person;
        }

        public override Int32 GetHashCode()
            => ((m_Person.LastName.GetHashCode() / 4)
                + (m_Person.FirstName.GetHashCode() / 4)
                + (m_Person.MiddleName.GetHashCode() / 4)
                + (m_Person.BirthYear.GetHashCode() / 4));

        public override Boolean Equals(Object obj)
        {
            PersonKey other = obj as PersonKey;

            if (other == null)
            {
                return (false);
            }
            else
            {
                return ((m_Person.LastName == other.Person.LastName)
                    && (m_Person.FirstName == other.Person.FirstName)
                    && (m_Person.MiddleName == other.Person.MiddleName)
                    && (m_Person.BirthYear == other.Person.BirthYear));
            }
        }

        public override String ToString()
        {
            StringBuilder name = new StringBuilder();

            if (String.IsNullOrEmpty(m_Person.FirstName) == false)
            {
                name.Append("<" + m_Person.FirstName + ">");
            }

            if (String.IsNullOrEmpty(m_Person.MiddleName) == false)
            {
                if (name.Length != 0)
                {
                    name.Append(" ");
                }

                name.Append("{" + m_Person.MiddleName + "}");
            }

            if (String.IsNullOrEmpty(m_Person.LastName) == false)
            {
                if (name.Length != 0)
                {
                    name.Append(" ");
                }

                name.Append("[" + m_Person.LastName + "]");
            }

            if (m_Person.BirthYear != 0)
            {
                if (name.Length != 0)
                {
                    name.Append(" ");
                }

                name.Append("(" + m_Person.BirthYear + ")");
            }

            return (name.ToString());
        }
    }

    internal class Name
    {
        public StringBuilder FirstName = new StringBuilder();

        public StringBuilder MiddleName = new StringBuilder();

        public StringBuilder LastName = new StringBuilder();
    }
}