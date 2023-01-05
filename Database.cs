using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistillNET.DistillNET
{
	internal class Database
	{

		/*
		 * Table Columns:
		 *   | domain | categoryId | isWhitelist | source |
		 * 
		 * Domain key for non-domain-specific rules is "global"
		 */

		private static readonly HashSet<DatabaseEntry> EmptyEntrySet = new HashSet<DatabaseEntry>(0);

		private Dictionary<bool, Dictionary<string, HashSet<DatabaseEntry>>> dictionary;

		public Database()
		{

			dictionary = new Dictionary<bool, Dictionary<string, HashSet<DatabaseEntry>>>();

			dictionary[true] = new Dictionary<string, HashSet<DatabaseEntry>>();
			dictionary[false] = new Dictionary<string, HashSet<DatabaseEntry>>();

		}

		public void addEntry(DatabaseEntry entry)
		{

			HashSet<DatabaseEntry> set = dictionary[entry.IsWhitelist].GetValueOrDefault(entry.Domain, null);

			if (set == null)
			{
				set = new HashSet<DatabaseEntry>();
				dictionary[entry.IsWhitelist][entry.Domain] = set;
			}

			set.Add(entry);

		}

		public HashSet<DatabaseEntry> getEntries(string domain, bool isWhitelisted)
		{

			return dictionary[isWhitelisted].GetValueOrDefault(domain, EmptyEntrySet);

		}

	}
}
