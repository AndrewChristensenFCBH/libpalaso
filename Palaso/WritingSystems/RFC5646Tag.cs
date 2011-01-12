﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Palaso.WritingSystems
{
	public class RFC5646Tag : Object
	{
		public enum SubTag
		{
			Language,
			Script,
			Region,
			Variant
		}

		private List<string> _language;
		private List<string> _script;
		private List<string> _region;
		private List<string> _variant;

		public RFC5646Tag(string language, string script, string region, string variant)
		{
			Language = language;
			Script = script;
			Region = region;
			Variant = variant;
		}

		///<summary>
		/// Copy constructor
		///</summary>
		///<param name="rhs"></param>
		public RFC5646Tag(RFC5646Tag rhs)
		{
			_language = rhs._language;
			_script = rhs._script;
			_region = rhs._region;
			_variant = rhs._variant;
		}

		public string CompleteTag
		{
			get
			{
				string id = String.IsNullOrEmpty(Language) ? string.Empty : Language;
				if (!String.IsNullOrEmpty(Script))
				{
					id += "-" + Script;
				}
				if (!String.IsNullOrEmpty(Region))
				{
					id += "-" + Region;
				}
				if (!String.IsNullOrEmpty(Variant))
				{
					id += "-" + Variant;
				}
				return id;
			}
		}

		public string Language
		{
			get { return AssembleLanguageSubtag(_language); }
			set { _language = ParseSubtagForParts(value); }
		}

		public string Script
		{
			get { return AssembleLanguageSubtag(_script); }
			set { _script = ParseSubtagForParts(value); }
		}

		public string Region
		{
			get { return AssembleLanguageSubtag(_region); }
			set { _region = ParseSubtagForParts(value); }
		}

		public string Variant
		{
			get { return AssembleLanguageSubtag(_variant); }
			set { _variant = ParseSubtagForParts(value); }
		}

		public void AddToSubtag(SubTag subTag, string stringToAppend)
		{
			List<string> SubtagToAddTo = GetSubtagList(subTag);
			SubtagToAddTo.Add("-");
			SubtagToAddTo.Add(stringToAppend);//= AddToSubtag(_language, stringToAppend);
		}

		private List<string> GetSubtagList(SubTag subTag)
		{
			List<string> SubtagToAddTo = new List<string>();
			switch (subTag)
			{
				case SubTag.Language:
					SubtagToAddTo = _language;
					break;
				case SubTag.Script:
					SubtagToAddTo = _language;
					break;
				case SubTag.Region:
					SubtagToAddTo = _language;
					break;
				case SubTag.Variant:
					SubtagToAddTo = _language;
					break;
				default: throw new ApplicationException();
			}
			return SubtagToAddTo;
		}

		private string AddToSubtag(string currentSubTagValue, string stringToAppend)
		{
			bool subtagAlreadyContainsStringToAppend = currentSubTagValue.Contains(stringToAppend, StringComparison.OrdinalIgnoreCase);
			if(subtagAlreadyContainsStringToAppend)
			{
				throw new ArgumentException(String.Format("The subtag already contains a string {0}", stringToAppend));
			}
			if(String.IsNullOrEmpty(currentSubTagValue))
			{
				return stringToAppend;
			}
			return currentSubTagValue + "-" + stringToAppend;
		}

		///<summary>
		// This method defines what is currently considered a valid RFC 5646 language tag by palaso.
		// At the moment this is almost anything.
		///</summary>
		///<returns></returns>
		public bool IsValid()
		{
			//if (IsBadAudioTag(this))
			//{
			//    return false;
			//}
			return true;
		}

		public static RFC5646Tag GetValidTag(RFC5646Tag tagToConvert)
		{
			if (tagToConvert.IsValid()) { return tagToConvert; }

			RFC5646Tag validRfc5646Tag = null;

			if (IsBadAudioTag(tagToConvert))
			{
				string newLanguageTag = tagToConvert.Language.Split('-')[0];
				validRfc5646Tag = RFC5646TagForVoiceWritingSystem(newLanguageTag, tagToConvert.Region);
			}
			if (validRfc5646Tag == null || !validRfc5646Tag.IsValid())
			{
				throw new InvalidOperationException("The palaso library is not able to covert this tag to a valid form.");
			}
			return validRfc5646Tag;
		}

		private static bool IsBadAudioTag(RFC5646Tag tagToConvert)
		{
			return (tagToConvert.Language.Contains("x-audio")) ||
				   (tagToConvert.Variant == "x-audio" && tagToConvert.Script != "Zxxx") ||
				   (tagToConvert.Variant == "x-audio" && tagToConvert.Language.Contains("-"));
		}

		public static RFC5646Tag RFC5646TagForVoiceWritingSystem(string language, string region)
		{
			return new RFC5646Tag(language, "Zxxx", region, "x-audio");
		}

		public string ToString()
		{
			return CompleteTag;
		}

		public override bool Equals(Object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof (RFC5646Tag)) return false;
			return Equals((RFC5646Tag) obj);
		}

		public bool Equals(RFC5646Tag other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(other._language, _language) && Equals(other._script, _script) && Equals(other._region, _region) && Equals(other._variant, _variant);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int result = (_language != null ? _language.GetHashCode() : 0);
				result = (result*397) ^ (_script != null ? _script.GetHashCode() : 0);
				result = (result*397) ^ (_region != null ? _region.GetHashCode() : 0);
				result = (result*397) ^ (_variant != null ? _variant.GetHashCode() : 0);
				return result;
			}
		}

		public void RemoveFromSubtag(SubTag subTag, string stringToRemove)
		{
			List<string> SubtagToRemovePartFrom = GetSubtagList(subTag);
			int indexOfpartToRemove = SubtagToRemovePartFrom.FindIndex(part => part == stringToRemove);
			bool stringToRemoveIsOnlyPartOfSubtag = (SubtagToRemovePartFrom.Count == 1) &&
													(SubtagToRemovePartFrom[0].Equals(stringToRemove,StringComparison.OrdinalIgnoreCase));
			bool stringToRemoveIsFirstPartOfMultiPartSubtag =
			if(stringToRemoveIsOnlyPartOfSubtag)
			{
				SubtagToRemovePartFrom.RemoveAt(indexOfpartToRemove);
				SubtagToRemovePartFrom.RemoveAt(indexOfpartToRemove);
			}
		}

		private string RemoveFromSubtag(string currentSubtagValue, string stringToRemove)
		{
			string stringToReturn = String.Empty;

			bool subTagContainsOnlyStringtoRemove = currentSubtagValue.Equals(stringToRemove, StringComparison.OrdinalIgnoreCase);

			if (subTagContainsOnlyStringtoRemove)
			{
				stringToReturn = String.Empty;
			}
			else
			{

				int positionInSubtagOfStringToRemove = currentSubtagValue.IndexOf(stringToRemove,StringComparison.OrdinalIgnoreCase);
				bool stringToRemoveIsFirstInSubtag = (positionInSubtagOfStringToRemove == 0);

				if (stringToRemoveIsFirstInSubtag)
				{
					currentSubtagValue.Remove(positionInSubtagOfStringToRemove, stringToRemove.Length);
					currentSubtagValue.TrimEnd(seperators);
				}
				else
				{
					currentSubtagValue.Remove(positionInSubtagOfStringToRemove, stringToRemove.Length);
					currentSubtagValue.TrimStart(seperators);
				}
			}
			return stringToReturn;
		}

		public List<string> ParseSubtagForParts(string subtagToParse)
		{
			for (int i = 0; i++;i<subtagToParse.Length )
			{

			}
				throw new NotImplementedException();
		}

		private string AssembleLanguageSubtag(List<string> region)
		{
			throw new NotImplementedException();
		}
	}
}
