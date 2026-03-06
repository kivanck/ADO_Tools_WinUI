using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ADO_Tools.Utilities
{

    public class wordCompare
    {
        public int compareStrings(string str1, string str2)
        {

            string[] str1Words = stringProcess(str1);
            string[] str2Words = stringProcess(str2);


            int str1Match = 0;
            foreach (string str in str1Words)
            {
                if (str2.ToUpper().Contains(str))
                {
                    str1Match++;
                }
            }

            int str2Match = 0;
            foreach (string str in str2Words)
            {
                if (str1.ToUpper().Contains(str))
                {
                    str2Match++;
                }
            }

            int maxMatch = 0;

            maxMatch = str1Match > str2Match ? str1Match : str2Match;

            return maxMatch;
        }

        private string[] stringProcess(string strInput)
        {
            //make upper case
            strInput = strInput.ToUpper();

            //Remove special chracters
            strInput = Regex.Replace(strInput, @"[^\w\d\s]", " ");


            char[] charSeparators = new char[] { ' ' };
            string[] strWords = strInput.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);

            //Remove common words
            List<string> strList = strWords.ToList();
            for (int i = strList.Count - 1; i >= 0; i--)
            {
                string str = strList[i];
                if (str.Length < 2 || str.Equals("AND") || str.Equals("OR") || str.Equals("NOT")
                    || str.Equals("IN") || str.Equals("ON") || str.Equals("OF")
                    || str.Equals("A") || str.Equals("THAT") || str.Equals("TO")
                    || str.Equals("THE") || str.Equals("BUT") || str.Equals("BY")
                    || str.Equals("AN") || str.Equals("IS") || str.Equals("FOR")
                    || str.Equals("FROM") || str.Equals("WITH") || str.Equals("WHEN")
                    || str.Equals("WHILE") || str.Equals("THEN") || str.Equals("THAT")
                    || str.Equals("USE") || str.Equals("USING") || str.Equals("THAT"))
                {
                    strList.RemoveAt(i);
                }



            }

           return strList.Distinct().ToArray();

        }
    }

    /// <summary>
    /// This class implements string comparison algorithm
    /// based on character pair similarity
    /// Source: http://www.catalysoft.com/articles/StrikeAMatch.html
    /// </summary>
    public class SimilarityTool
    {
        /// <summary>
        /// Compares the two strings based on letter pair matches
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        /// <returns>The percentage match from 0.0 to 1.0 where 1.0 is 100%</returns>
        public double CompareStrings(string str1, string str2)
        {
            List<string> pairs1 = WordLetterPairs(str1.ToUpper());
            List<string> pairs2 = WordLetterPairs(str2.ToUpper());

            int intersection = 0;
            int union = pairs1.Count + pairs2.Count;

            for (int i = 0; i < pairs1.Count; i++)
            {
                for (int j = 0; j < pairs2.Count; j++)
                {
                    if (pairs1[i] == pairs2[j])
                    {
                        intersection++;
                        pairs2.RemoveAt(j);//Must remove the match to prevent "GGGG" from appearing to match "GG" with 100% success

                        break;
                    }
                }
            }

            return 2.0 * intersection / union;
        }

        /// <summary>
        /// Gets all letter pairs for each
        /// individual word in the string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private List<string> WordLetterPairs(string str)
        {
            List<string> AllPairs = new List<string>();

            // Tokenize the string and put the tokens/words into an array
            string[] Words = Regex.Split(str, @"\s");

            // For each word
            for (int w = 0; w < Words.Length; w++)
            {
                if (!string.IsNullOrEmpty(Words[w]))
                {
                    // Find the pairs of characters
                    string[] PairsInWord = LetterPairs(Words[w]);

                    for (int p = 0; p < PairsInWord.Length; p++)
                    {
                        AllPairs.Add(PairsInWord[p]);
                    }
                }
            }

            return AllPairs;
        }

        /// <summary>
        /// Generates an array containing every 
        /// two consecutive letters in the input string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string[] LetterPairs(string str)
        {
            int numPairs = str.Length - 1;

            string[] pairs = new string[numPairs];

            for (int i = 0; i < numPairs; i++)
            {
                pairs[i] = str.Substring(i, 2);
            }

            return pairs;
        }
    }

}
