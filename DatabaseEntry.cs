using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistillNET.DistillNET
{
	internal class DatabaseEntry
	{

		public string Domain { get; }

		public short CategoryId { get; }

		public bool IsWhitelist { get; }

		public string Source { get; }

		public DatabaseEntry(string domain, short categoryId, bool isWhitelist, string source)
		{
			Domain = domain;
			CategoryId = categoryId;
			IsWhitelist = isWhitelist;
			Source = source;
		}

	}
}
