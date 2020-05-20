using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Data.SQLite;

namespace spacedout
{
    public class Phrase
    {
        public int ID { get; set; }
        public string Text { get; set; }
        public string Translation { get; set; }
        public string Tag { get; set; }

        // frequency = minutes

        // (now-timestamp)/60 >= frequency

        // frequency + (now-timestamp)/max >= 1

        // 1.0, 0.0 -> 1
        // 0.9, 0.0 -> 0
        // 0.9, 0.1 -> 1
        // 0.9, 0.5 -> 1
        // 0.8, 0.0 -> 0
        // 0.8, 0.1 -> 0
        // 0.8, 0.2 -> 0

        // 0.1, 0.0 -> 0
        // 0.1, 0.9 -> 0
        // 0.0, 1.0 -> 1

        // 0 -> every min
        // 0.5 -> appears half the time
        // 1 -> once per max
        public float Frequency { get; set; }

        public int Timestamp { get; set; }

        //public static float MaxWait = 86400.0f * 2.0f;
        public static int Limit = 30;

        public void DecreaseFrequency(float minutes)
        {
            Frequency += minutes;
        }

        //public void SetMaxFrequency(float delta)
        //{
        //    var freq = 1f - delta;
        //    if (Frequency > freq)
        //    {
        //        Frequency = freq;
        //    }
        //}

        public void IncreaseFrequency(float minutes)
        {
            Frequency -= minutes;
            if (Frequency < 0f)
            {
                Frequency = 0f;
            }
        }
    }

    public class PhraseImage
    {
        public long Size { get; set; }
        public Stream Stream { get; set; }
    }

    public class Db
    {
        public void Insert(Phrase phrase)
        {
        }

        public bool IsQuizTime()
        {

            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    SELECT `value` FROM `stat` WHERE `key` = 'last_quiz'
                ", conn))
            using (var r = sql.ExecuteReader())
            {
                if (r.Read())
                {
                    var lastQuiz = r.GetInt64("value");
                    var d = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastQuiz;
                    return d > 60 * 60;
                }
            }
            return false;
        }

        public IEnumerable<Phrase> GetPhrases(string tag = null)
        {
            var available = false;
            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                SELECT * FROM (
                    SELECT `id`, `text`, `translation`, COALESCE(`tag`, ''), `frequency`, `timestamp`
                    FROM `phrases` 
                    WHERE CAST(@now-`timestamp` AS FLOAT)/60 >= `frequency`
                        AND (@tag IS NULL OR `tag` = @tag)
                    ORDER BY `timestamp`
                    LIMIT @limit
                ) ORDER BY RANDOM()
                ", conn))
            {
                sql.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                sql.Parameters.AddWithValue("@tag", tag);
                sql.Parameters.AddWithValue("@limit", Phrase.Limit);


                using (var r = sql.ExecuteReader())
                {

                    while (r.Read())
                    {
                        available = true;
                        yield return new Phrase
                        {
                            ID = r.GetInt32(0),
                            Text = r.GetString(1),
                            Translation = r.GetString(2),
                            Tag = r.GetString(3),
                            Frequency = r.GetFloat(4),
                            Timestamp = r.GetInt32(5),
                        };
                    }
                }
            }

            //if (!available)
            //{
            //    foreach (var p in GetRandomPhrases(tag))
            //    {
            //        yield return p;
            //    }
            //}
        }

        public IEnumerable<Phrase> GetRandomPhrases(string tag = null)
        {
            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    SELECT `id`, `text`, `translation`, COALESCE(`tag`, ''), `frequency`, `timestamp`
                    FROM `phrases` 
                    WHERE @tag IS NULL OR `tag` = @tag
                    ORDER BY random()
                    LIMIT @limit
                ", conn))
            {
                sql.Parameters.AddWithValue("@tag", tag);
                sql.Parameters.AddWithValue("@limit", Phrase.Limit);

                using (var r = sql.ExecuteReader())
                {
                    while (r.Read())
                    {
                        yield return new Phrase
                        {
                            ID = r.GetInt32(0),
                            Text = r.GetString(1),
                            Translation = r.GetString(2),
                            Tag = r.GetString(3),
                            Frequency = r.GetFloat(4),
                            Timestamp = r.GetInt32(5),
                        };
                    }
                }
            }
        }

        public void UpdateTime(Phrase phrase)
        {
            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    UPDATE `phrases` SET `timestamp` = strftime('%s','now') WHERE `id` = @id
                ", conn))
            {

                sql.Parameters.AddWithValue("@id", phrase.ID);
                sql.ExecuteNonQuery();
            }
        }

        public void IncreaseFrequency(Phrase phrase, float minutes)
        {
            phrase.IncreaseFrequency(minutes);
            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    UPDATE `phrases` SET `frequency` = @freq, `timestamp` = strftime('%s','now') WHERE `id` = @id
                ", conn))
            {

                sql.Parameters.AddWithValue("@freq", phrase.Frequency);
                sql.Parameters.AddWithValue("@id", phrase.ID);
                sql.ExecuteNonQuery();
            }
        }

        //public void SetMaxFrequency(Phrase phrase, float delta)
        //{
        //    phrase.SetMaxFrequency(delta);
        //    using (var conn = createConnection())
        //    using (var sql = new SQLiteCommand(@"
        //            UPDATE `phrases` SET `frequency` = @freq, `timestamp` = strftime('%s','now') WHERE `id` = @id
        //        ", conn))
        //    {

        //        sql.Parameters.AddWithValue("@freq", phrase.Frequency);
        //        sql.Parameters.AddWithValue("@id", phrase.ID);
        //        sql.ExecuteNonQuery();
        //    }
        //}

        public void DecreaseFrequency(Phrase phrase, float minutes)
        {
            phrase.DecreaseFrequency(minutes);
            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    UPDATE `phrases` SET `frequency` = @freq, `timestamp` = strftime('%s','now') WHERE `id` = @id
                ", conn))
            {

                sql.Parameters.AddWithValue("@freq", phrase.Frequency);
                sql.Parameters.AddWithValue("@id", phrase.ID);
                sql.ExecuteNonQuery();
            }
        }

        public Stream GetImageStream(Phrase phrase)
        {

            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    SELECT `image` FROM `phrases` WHERE `id` = @id
                ", conn))
            {

                sql.Parameters.AddWithValue("@id", phrase.ID);
                using (var r = sql.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return r.GetStream(0);
                    }
                }
            }
            return null;
        }

        public byte[] readImage(Phrase phrase)
        {
            foreach (var ext in new string[] { "png", "jpg", "jpeg", "bmp" })
            {
                try
                {
                    var filename = $"{phrase.ID.ToString()}.{ext}";
                    using (var stream = File.Open(Path.Combine("images", filename), FileMode.Open))
                    using (var reader = new BinaryReader(stream))
                    {
                        return reader.ReadBytes((int)stream.Length);
                    }
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
            }
            return null;
        }

        public string getDBPath()
        {
            var appDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(appDir, "foobar.db");
        }

        public void createTables()
        {
            if (Environment.GetEnvironmentVariable("RESET_DB") != null)
            {
                File.Delete(getDBPath());
            }


            using (var conn = createConnection())
            using (var sql = new SQLiteCommand(@"
                    CREATE TABLE IF NOT EXISTS `phrases` (
                        `id` integer PRIMARY KEY,
                        `text` varchar(512) NOT NULL UNIQUE,
                        `translation` varchar(512) NOT NULL,
                        `tag` varchar(64),
                        `frequency` float(3) NOT NULL DEFAULT 1,
                        `timestamp` int(10) NOT NULL DEFAULT 0,
                        `image` blob
                    );
                ", conn))
            {

                sql.ExecuteNonQuery();

                var seed = new Phrase[] {
                    new Phrase{ID = 1, Text = "settings", Translation = "inställingar", },
                    new Phrase{ID = 2, Text = "aosdijf", Translation = "tillbehör", },
                    new Phrase{ID = 3, Text = "iasdoifj", Translation = "utbildning", },
                    new Phrase{ID = 4, Text = "Others", Translation = "Övrigt", },
                    new Phrase{ID = 4, Text = "8123", Translation = "andvändare och grupper", },
                    new Phrase{ID = 5, Text = "812j", Translation = "Platser", },
                    new Phrase{ID = 6, Text = "8129", Translation = "Arbetsytor", },
                    new Phrase{ID = 7, Text = "812o", Translation = "Arkivhanterare", },
                };


                sql.CommandText = "INSERT OR IGNORE INTO `phrases`(`id`, `text`, `translation`, `tag`, `timestamp`, `image`) VALUES(@id, @text, @tr, @tag, strftime('%s','now'), @image)";
                foreach (var row in seed)
                {
                    sql.Parameters.AddWithValue("@id", row.ID);
                    sql.Parameters.AddWithValue("@text", row.Text);
                    sql.Parameters.AddWithValue("@tr", row.Translation);
                    sql.Parameters.AddWithValue("@tag", row.Tag);
                    sql.Parameters.AddWithValue("@image", readImage(row));

                    var image = readImage(row);
                    if (image != null)
                    {
                        var par = sql.CreateParameter();
                        par.ParameterName = "@image";
                        par.DbType = DbType.Binary;
                        par.Value = image;
                        sql.Parameters.Add(par);
                        sql.ExecuteNonQuery();
                    }
                    else
                    {
                        sql.ExecuteNonQuery();
                    }

                }
            }
        }

        public SQLiteConnection createConnection()
        {
            var b = new SQLiteConnectionStringBuilder();
            b.DataSource = getDBPath();
            var conn = new SQLiteConnection(b.ConnectionString);
            conn.Open();
            return conn;
        }
    }


    // enable next button after 10 seconds
    // switch phrase every 20 seconds
    // apply space-repetition when show or next button is clicked 

    public interface Translater
    {
        string time(DateTime t);
        string number(int n);

        string weekDay(DayOfWeek day);

        string month(int m);
        string word(string w);


        //Dictionary<string, string> getPhraseList(string w);
        char[] chars();
    }

    public class Swedish : Translater
    {
        public string time(DateTime t)
        {
            var hour = t.Hour;
            var minutes = t.Minute;
            var seconds = t.Second;

            return string.Format("{0}:{1}:{2}", number(hour % 12), number(minutes), number(seconds));
        }
        public string weekDay(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return "måndag";
                case DayOfWeek.Tuesday: return "tisdag";
                case DayOfWeek.Wednesday: return "onsdag";
                case DayOfWeek.Thursday: return "torsdag";
                case DayOfWeek.Friday: return "fredag";
                case DayOfWeek.Saturday: return "lördag";
                case DayOfWeek.Sunday: return "söndag";
            }
            return "*";
        }

        public string word(string w)
        {
            switch (w)
            {
                case "time": return "tid";
                case "weekday": return "veckodag";
                case "month": return "månad";
            }
            return "*";
        }

        public string month(int m)
        {
            switch (m)
            {
                case 1: return "januari";
                case 2: return "februari";
                case 3: return "mars";
                case 4: return "april";
                case 5: return "maj";
                case 6: return "juni";
                case 7: return "juli";
                case 8: return "augusti";
                case 9: return "september";
                case 10: return "oktober";
                case 11: return "november";
                case 12: return "december";
            }
            return "*";
        }

        public string number(int n)
        {
            switch (n)
            {
                case 0: return "noll";
                case 1: return "ett";
                case 2: return "två";
                case 3: return "tre";
                case 4: return "fyra";
                case 5: return "fem";
                case 6: return "sex";
                case 7: return "sju";
                case 8: return "åtta";
                case 9: return "nio";
                case 10: return "tio";
                case 11: return "elva";
                case 12: return "tolv";
                case 13: return "tretton";
                case 14: return "fjorton";
                case 15: return "femton";
                case 16: return "sexton";
                case 17: return "sjutton";
                case 18: return "arton";
                case 19: return "nitton";
                case 20: return "tjugo";
                case 30: return "trettio";
                case 40: return "fyrtio";
                case 50: return "femtio";
                case 60: return "sextio";
                case 70: return "sjuttio";
                case 80: return "åttio";
                case 90: return "nittio";
                case 100: return "ett hundra";
            }
            if (n >= 21 && n <= 99)
            {
                var b = n % 10;
                var a = n - b;
                return number(a) + number(b);
            }
            return "*";
        }

        public char[] chars()
        {
            return new char[] { 'å', 'ä', 'ö', 'Å', 'Ä', 'Ö' };
        }
    }
}