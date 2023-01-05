/*
 * Copyright © 2017 Jesse Nicholson
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using DistillNET.DistillNET;
using DistillNET.Extensions;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;

namespace DistillNET
{
	/// <summary>
	/// The FilterDbCollection class is responsible for parsing and storing rules, with associated
	/// category ID's, into a database. The class is also responsible for calling up collections of
	/// rules on the fly for any given, known domain. Called up rules are re-parsed at every lookup
	/// rather than serialized/deserialized, because the parser is much faster than such utilities
	/// such as protobuf.
	/// </summary>
	public class FilterDbCollection
	{

		/// <summary>
		/// Our rule parser.
		/// </summary>
		private AbpFormatRuleParser m_ruleParser;

		/// <summary>
		/// The global key used to index non-domain specific filters.
		/// </summary>
		private readonly string m_globalKey;

		/// <summary>
		/// Memory cache.
		/// </summary>
		private MemoryCache m_cache;

		/// <summary>
		/// Mem cache options.
		/// </summary>
		private readonly MemoryCacheOptions m_cacheOptions;

		private Database database;

		private bool locked;

		/// <summary>
		/// Constructs a new FilterDbCollection.
		/// </summary>
		/// <param name="cacheOptions">
		/// User defined query caching options.
		/// </param>
		public FilterDbCollection(MemoryCacheOptions cacheOptions = null)
		{

			if (cacheOptions == null)
			{
				cacheOptions = new MemoryCacheOptions
				{
					ExpirationScanFrequency = TimeSpan.FromMinutes(10)
				};
			}

			m_cacheOptions = cacheOptions;

			RecreateCache();

			m_ruleParser = new AbpFormatRuleParser();

			m_globalKey = "global";

			database = new Database();

			locked = false;
		}

		/// <summary>
		/// Prevents further filters from being added.
		/// </summary>
		public void FinalizeForRead()
		{
			locked = true;
		}

		private void RecreateCache()
		{
			if (m_cache != null)
			{
				m_cache.Dispose();
			}

			m_cache = new MemoryCache(m_cacheOptions);
		}

		/// <summary>
		/// Parses the supplied list of rules and stores them in the assigned database for retrieval,
		/// indexed by the rule's domain names.
		/// </summary>
		/// <param name="rawRuleStrings">
		/// The raw filter strings.
		/// </param>
		/// <param name="categoryId">
		/// The category ID that each of the supplied filters is deemeded to belong to.
		/// </param>
		/// <returns>
		/// A tuple where the first item is the total number of rules successfully parsed and stored,
		/// and the second item is the total number of rules that failed to be parsed and stored.
		/// Failed rules are an indication of improperly formatted rules.
		/// </returns>
		public Tuple<int, int> ParseStoreRules(string[] rawRuleStrings, short categoryId)
		{
			if (locked)
			{
				throw new InvalidOperationException("Cannot add rules after being locked.");
			}

			RecreateCache();

			int loaded = 0, failed = 0;

			var len = rawRuleStrings.Length;
			for (int i = 0; i < len; ++i)
			{
				rawRuleStrings[i] = rawRuleStrings[i].TrimQuick();

				if (!(m_ruleParser.ParseAbpFormattedRule(rawRuleStrings[i], categoryId) is UrlFilter filter))
				{
					++failed;
					continue;
				}

				++loaded;

				if (filter.ApplicableDomains.Count > 0)
				{
					foreach (var dmn in filter.ApplicableDomains)
					{
						database.addEntry(new DatabaseEntry(dmn, categoryId, filter.IsException, rawRuleStrings[i]));
					}
				}
				else
				{
					database.addEntry(new DatabaseEntry(m_globalKey, categoryId, filter.IsException, rawRuleStrings[i]));
				}
			}

			return new Tuple<int, int>(loaded, failed);
		}

		/// <summary>
		/// Parses the supplied list of rules and stores them in the assigned database for retrieval,
		/// indexed by the rule's domain names.
		/// </summary>
		/// <param name="rawRulesStream">
		/// The stream from which to read raw rules as lines.
		/// </param>
		/// <param name="categoryId">
		/// The category ID that each of the supplied filters is deemeded to belong to.
		/// </param>
		/// <returns>
		/// A tuple where the first item is the total number of rules successfully parsed and stored,
		/// and the second item is the total number of rules that failed to be parsed and stored.
		/// Failed rules are an indication of improperly formatted rules.
		/// </returns>
		public Tuple<int, int> ParseStoreRulesFromStream(Stream rawRulesStream, short categoryId)
		{
			if (locked)
			{
				throw new InvalidOperationException("Cannot add rules after being locked.");
			}

			RecreateCache();

			int loaded = 0, failed = 0;

			string line = null;
			using var sw = new StreamReader(rawRulesStream);
			while ((line = sw.ReadLine()) != null)
			{
				line = line.TrimQuick();

				if (!(m_ruleParser.ParseAbpFormattedRule(line, categoryId) is UrlFilter filter))
				{
					++failed;
					continue;
				}

				++loaded;

				if (filter.ApplicableDomains.Count > 0)
				{
					foreach (var dmn in filter.ApplicableDomains)
					{
						database.addEntry(new DatabaseEntry(dmn, categoryId, filter.IsException, line));
					}
				}
				else
				{
					database.addEntry(new DatabaseEntry(m_globalKey, categoryId, filter.IsException, line));
				}
			}

			return new Tuple<int, int>(loaded, failed);
		}

		/// <summary>
		/// Gets all blacklisting filters for the supplied domain.
		/// </summary>
		/// <param name="domain">
		/// The domain for which all filters should be loaded. Default is global, meaning that
		/// filters not anchored to any particular domain will be loaded.
		/// </param>
		/// <returns>
		/// A list of all compiled blacklisting URL filters for the given domain.
		/// </returns>
		public IEnumerable<UrlFilter> GetFiltersForDomain(string domain = "global")
		{
			return GetFiltersForDomain(domain, false);
		}

		/// <summary>
		/// Gets all whitelisting filters for the supplied domain.
		/// </summary>
		/// <param name="domain">
		/// The domain for which all filters should be loaded. Default is global, meaning that
		/// filters not anchored to any particular domain will be loaded.
		/// </param>
		/// <returns>
		/// A list of all compiled whitelisting URL filters for the given domain.
		/// </returns>
		public IEnumerable<UrlFilter> GetWhitelistFiltersForDomain(string domain = "global")
		{
			return GetFiltersForDomain(domain, true);
		}

		/// <summary>
		/// Gets a list of either all whitelist or all blacklist filters for the given domain.
		/// </summary>
		/// <param name="domain">
		/// The domain for which to retrieve all whitelist or blacklist filters.
		/// </param>
		/// <param name="isWhitelist">
		/// Whether or not to get whitelist filters. If false, blacklist filters will be selected.
		/// </param>
		/// <returns>
		/// A list of either all whitelist or all blacklist filters for the given domain.
		/// </returns>
		private IEnumerable<UrlFilter> GetFiltersForDomain(string domain, bool isWhitelist)
		{
			var cacheKey = new Tuple<string, bool>(domain, isWhitelist);

			if (m_cache.TryGetValue(cacheKey, out List<UrlFilter> retVal))
			{
				if (retVal != null)
				{
					foreach (var elm in retVal)
					{
						yield return elm;
					}

					yield break;
				}
			}

			retVal = new List<UrlFilter>();

			var allPossibleVariations = GetAllPossibleSubdomains(domain);

			foreach (var sub in allPossibleVariations)
			{
				foreach (DatabaseEntry entry in database.getEntries(sub, isWhitelist))
				{
					var newRule = (UrlFilter)m_ruleParser.ParseAbpFormattedRule(entry.Source, entry.CategoryId);
					retVal.Add(newRule);
				}
			}

			m_cache.Set(cacheKey, retVal);

			foreach (var elm in retVal)
			{
				yield return elm;
			}

			yield break;
		}

		private List<string> GetAllPossibleSubdomains(string inputDomain)
		{
			var retVal = new List<string>() { inputDomain };
			int subPos = inputDomain.IndexOfQuick('.');

			while (subPos != -1)
			{
				inputDomain = inputDomain.Substring(subPos + 1);
				retVal.Add(inputDomain);
				subPos = inputDomain.IndexOfQuick('.');
			}

			return retVal;
		}

	}
}