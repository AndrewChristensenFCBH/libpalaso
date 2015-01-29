using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Icu;
using Palaso.Extensions;
using Palaso.Xml;

namespace SIL.WritingSystems
{
	/// <summary>
	/// The LdmlDatamapper Reads and Writes WritingSystemDefinitions to LDML files. A typical consuming application should not
	/// need to use the LdmlDataMapper directly but should rather use an IWritingSystemRepository (such as the
	/// LdmlInfolderWritingSystemRepository) to manage it's writing systems.
	/// The LdmlDatamapper is tightly tied to a particular (palaso) version of LDML. If the LdmlDatamapper refuses to Read a
	/// particular Ldml file it may need to be migrated to the latest version. Please use the
	/// LdmlInFolderWritingSystemRepository class for this purpose.
	/// Please note that the LdmlDataMapper.Write method can round trip data that it does not understand if passed an
	/// appropriate stream or xmlreader produced from the old file.
	/// Be aware that as of Jul-5-2011 an exception was made for certain well defined Fieldworks LDML files whose contained
	/// Rfc5646 tag begin with "x-". These will load correctly, albeit in a transformed state, in spite of being "Version 0".
	/// Furthermore writing systems containing RfcTags beginning with "x-" and that have a matching Fieldworks conform LDML file
	/// in the repository will not be changed including no marking with "version 1".
	/// </summary>
	public class LdmlDataMapper
	{
		private bool _wsIsFlexPrivateUse;
#if WS_FIX
		private WritingSystemCompatibility _compatibilityMode;
#endif
		private static XNamespace Sil = "urn://www.sil.org/ldml/0.1";

		/// <summary>
		/// Mapping of font engine attribute to FontEngines enumeration.
		/// If this attribute is missing, the engines are assumed to be "gr ot"
		/// </summary>
		private static readonly Dictionary<string, FontEngines> EngineToFontEngines = new Dictionary<string, FontEngines>
		{
			{string.Empty, FontEngines.OpenType | FontEngines.Graphite },
			{"ot", FontEngines.OpenType},
			{"gr", FontEngines.Graphite}
		};

		/// <summary>
		/// Mapping of FontEngines enumeration to font engine attribute.
		/// If the engine is none, leave an empty string
		/// </summary>
		private static readonly Dictionary<FontEngines, string> FontEnginesToEngine = new Dictionary<FontEngines, string>
		{
			{FontEngines.None, string.Empty},
			{FontEngines.OpenType, "ot"},
			{FontEngines.Graphite, "gr"}
		};

		/// <summary>
		/// Mapping of font role/type attribute to FontRoles enumeration
		/// If this attribute is missing, the font role default is used
		/// </summary>
		private static readonly Dictionary<string, FontRoles> RoleToFontRoles = new Dictionary<string, FontRoles>
		{
			{string.Empty, FontRoles.Default},
			{"default", FontRoles.Default},
			{"heading", FontRoles.Heading},
			{"emphasis", FontRoles.Emphasis}
		};

		/// <summary>
		/// Mapping of FontRoles enumeration to font role/type attribute
		/// </summary>
		private static readonly Dictionary<FontRoles, string> FontRolesToRole = new Dictionary<FontRoles, string> 
		{
			{FontRoles.Default, "default"},
			{FontRoles.Heading, "heading"},
			{FontRoles.Emphasis, "emphasis"}
		};

		/// <summary>
		/// Mapping of spell checking type attribute to SpellCheckDictionaryFormat enumeration
		/// </summary>
		private static readonly Dictionary<string, SpellCheckDictionaryFormat> SpellCheckToSpecllCheckDictionaryFormats = new Dictionary
			<string, SpellCheckDictionaryFormat>
		{
			{string.Empty, SpellCheckDictionaryFormat.Unknown},
			{"hunspell", SpellCheckDictionaryFormat.Hunspell},
			{"wordlist", SpellCheckDictionaryFormat.Wordlist},
			{"lift", SpellCheckDictionaryFormat.Lift}
		};

		/// <summary>
		/// Mapping of SpellCheckDictionaryFormat enumeration to spell checking type attribute
		/// </summary>
		private static readonly Dictionary<SpellCheckDictionaryFormat, string> SpellCheckDictionaryFormatsToSpellCheck = new Dictionary
			<SpellCheckDictionaryFormat, string>
		{
			{SpellCheckDictionaryFormat.Unknown, string.Empty},
			{SpellCheckDictionaryFormat.Hunspell, "hunspell"},
			{SpellCheckDictionaryFormat.Wordlist, "wordlist"},
			{SpellCheckDictionaryFormat.Lift, "lift"}
		};

		/// <summary>
		/// Mapping of keyboard type attribute to KeyboardFormat enumeration
		/// </summary>
		private static readonly Dictionary<string, KeyboardFormat> KeyboardToKeyboardFormat = new Dictionary<string, KeyboardFormat>
		{
			{string.Empty, KeyboardFormat.Unknown},
			{"kmn", KeyboardFormat.Keyman },
			{"kmx", KeyboardFormat.CompiledKeyman },
			{"msklc", KeyboardFormat.Msklc},
			{"ldml", KeyboardFormat.Ldml},
			{"keylayout", KeyboardFormat.Keylayout}
		}; 

		/// <summary>
		/// Mapping of KeyboardFormat enumeration to keyboard type attribute
		/// </summary>
		private static readonly Dictionary<KeyboardFormat, string> KeyboardFormatToKeyboard = new Dictionary<KeyboardFormat, string>
		{
			{KeyboardFormat.Unknown, string.Empty},
			{KeyboardFormat.Keyman, "kmn"},
			{KeyboardFormat.CompiledKeyman, "kmx"},
			{KeyboardFormat.Msklc, "msklc"},
			{KeyboardFormat.Ldml, "ldml"},
			{KeyboardFormat.Keylayout, "keylayout"}
		}; 

		/// <summary>
		/// Mapping of context attribute to PunctuationPatternContext enumeration
		/// </summary>
		private static readonly Dictionary<string, PunctuationPatternContext> ContextToPunctuationPatternContext = new Dictionary<string, PunctuationPatternContext>
		{
			{"init", PunctuationPatternContext.Initial},
			{"medial", PunctuationPatternContext.Medial},
			{"final", PunctuationPatternContext.Final},
			{"break", PunctuationPatternContext.Break},
			{"isolate", PunctuationPatternContext.Isolate}
		};

		/// <summary>
		/// Mapping of PunctuationPatternContext enumeration to context attribute
		/// </summary>
		private static readonly Dictionary<PunctuationPatternContext, string> PunctuationPatternContextToContext = new Dictionary<PunctuationPatternContext, string>
		{
			{PunctuationPatternContext.Initial, "init"},
			{PunctuationPatternContext.Medial, "medial"},
			{PunctuationPatternContext.Final, "final"},
			{PunctuationPatternContext.Break, "break"},
			{PunctuationPatternContext.Isolate, "isolate"}
		};

		/// <summary>
		/// Mapping of paraContinueType attribute to QuotationParagraphContinueType enumeration
		/// </summary>
		private static readonly Dictionary<string, QuotationParagraphContinueType> QuotationToQuotationParagraphContinueTypes = new Dictionary<string, QuotationParagraphContinueType>
		{
			{string.Empty, QuotationParagraphContinueType.None},
			{"all", QuotationParagraphContinueType.All},
			{"outer", QuotationParagraphContinueType.Outermost},
			{"inner", QuotationParagraphContinueType.Innermost}
		};

		/// <summary>
		/// Mapping of QuotationParagraphContinueType enumeration to paraContinueType attribute
		/// </summary>
		private static readonly Dictionary<QuotationParagraphContinueType, string> QuotationParagraphContinueTypesToQuotation = new Dictionary<QuotationParagraphContinueType, string>
		{
			{QuotationParagraphContinueType.None, string.Empty},
			{QuotationParagraphContinueType.All, "all"},
			{QuotationParagraphContinueType.Outermost, "outer"},
			{QuotationParagraphContinueType.Innermost, "inner"}
		};

		/// <summary>
		/// Mapping of quotation marking system attribute to QuotationMarkingSystemType enumeration
		/// </summary>
		private static readonly Dictionary<string, QuotationMarkingSystemType>QuotationToQuotationMarkingSystemTypes = new Dictionary<string, QuotationMarkingSystemType>
		{
			{string.Empty, QuotationMarkingSystemType.Normal},
			{"narrative", QuotationMarkingSystemType.Narrative}
		};

		/// <summary>
		/// Mapping of QuotationMarkingSystemType enumeration to quotation marking system attribute
		/// </summary>
		private static readonly Dictionary<QuotationMarkingSystemType, string> QuotationMarkingSystemTypesToQuotation = new Dictionary<QuotationMarkingSystemType, string>
		{
			{QuotationMarkingSystemType.Normal, string.Empty},
			{QuotationMarkingSystemType.Narrative, "narrative"}
		};

		public void Read(string filePath, WritingSystemDefinition ws)
		{
			if (filePath == null)
			{
				throw new ArgumentNullException("filePath");
			}
			if (ws == null)
			{
				throw new ArgumentNullException("ws");
			}
			XElement element = XElement.Load(filePath);
			ReadLdml(element, ws);
		}

		public void Read(XmlReader xmlReader, WritingSystemDefinition ws)
		{
			if (xmlReader == null)
			{
				throw new ArgumentNullException("xmlReader");
			}
			if (ws == null)
			{
				throw new ArgumentNullException("ws");
			}
			var settings = new XmlReaderSettings
			{
				ConformanceLevel = ConformanceLevel.Auto,
				ValidationType = ValidationType.None,
				XmlResolver = null,
				DtdProcessing = DtdProcessing.Parse
			};
			using (XmlReader reader = XmlReader.Create(xmlReader, settings))
			{
				XElement element = XElement.Load(reader);
				ReadLdml(element, ws);
			}
		}

		public static void WriteLdmlText(XmlWriter writer, string text)
		{
			// Not all Unicode characters are valid in an XML document, so we need to create
			// the <cp hex="X"> elements to replace the invalid characters.
			// Note: While 0xD (carriage return) is a valid XML character, it is automatically
			// either dropped or converted to 0xA by any conforming XML parser, so we also make a <cp>
			// element for that one.
			StringBuilder sb = new StringBuilder(text.Length);
			for (int i=0; i < text.Length; i++)
			{
				int code = Char.ConvertToUtf32(text, i);
				if ((code == 0x9) ||
					(code == 0xA) ||
					(code >= 0x20 && code <= 0xD7FF) ||
					(code >= 0xE000 && code <= 0xFFFD) ||
					(code >= 0x10000 && code <= 0x10FFFF))
				{
					sb.Append(Char.ConvertFromUtf32(code));
				}
				else
				{
					writer.WriteString(sb.ToString());
					writer.WriteStartElement("cp");
					writer.WriteAttributeString("hex", String.Format("{0:X}", code));
					writer.WriteEndElement();
					sb = new StringBuilder(text.Length - i);
				}

				if (Char.IsSurrogatePair(text, i))
				{
					i++;
				}
			}
			writer.WriteString(sb.ToString());
		}

		private void ReadLdml(XElement element, WritingSystemDefinition ws)
		{
			Debug.Assert(element != null);
			Debug.Assert(ws != null);
			if (element.Name != "ldml")
			{
				throw new ApplicationException("Unable to load writing system definition: Missing <ldml> tag.");
			}

			XElement identityElem = element.Element("identity");
			if (identityElem != null)
				ReadIdentityElement(identityElem, ws);

			XElement charactersElem = element.Element("characters");
			if (charactersElem != null)
				ReadCharacterElement(charactersElem, ws);

			XElement delimitersElem = element.Element("delimiters");
			if (delimitersElem != null)
				ReadDelimitersElement(delimitersElem, ws);

			XElement layoutElem = element.Element("layout");
			if (layoutElem != null)
				ReadLayoutElement(layoutElem, ws);

			XElement numbersElem = element.Element("numbers");
			if (numbersElem != null)
				ReadNumbersElement(numbersElem, ws);

			XElement collationsElem = element.Element("collations");
			if (collationsElem != null)
				ReadCollationsElement(collationsElem, ws);

			foreach (XElement specialElem in element.Elements("special"))
			{
				ReadTopLevelSpecialElement(specialElem, ws);
			}
			ws.StoreID = "";
			ws.AcceptChanges();
		}

		private void ReadTopLevelSpecialElement(XElement specialElem, WritingSystemDefinition ws)
		{
			XElement externalResourcesElem = specialElem.Element(Sil + "external-resources");
			if (externalResourcesElem != null)
			{
				ReadFontElement(externalResourcesElem, ws);
				ReadSpellcheckElement(externalResourcesElem, ws);
				ReadKeyboardElement(externalResourcesElem, ws);
			}
		}

		private void ReadFontElement(XElement externalResourcesElem, WritingSystemDefinition ws)
		{
			foreach (XElement fontElem in externalResourcesElem.Elements(Sil + "font"))
			{
				string fontName = fontElem.GetAttributeValue("name");
				if (!fontName.Equals(string.Empty))
				{
					FontDefinition fd = new FontDefinition(fontName);

					// Types (space separate list)
					string roles = fontElem.GetAttributeValue("types");
					if (!String.IsNullOrEmpty(roles))
					{
						IEnumerable<string> roleList = roles.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
						foreach (string roleEntry in roleList)
						{
							fd.Roles |= RoleToFontRoles[roleEntry];
						}
					}
					else
					{
						fd.Roles = FontRoles.Default;
					}

					// Relative Size
					fd.DefaultRelativeSize = (float?) fontElem.Attribute("size") ?? 1.0f;

					// Minversion
					fd.MinVersion = fontElem.GetAttributeValue("minversion");

					// Features (space separated list of key=value pairs)
					fd.Features = fontElem.GetAttributeValue("features");

					// Language
					fd.Language = fontElem.GetAttributeValue("lang");

					// OpenType language
					fd.OpenTypeLanguage = fontElem.GetAttributeValue("otlang");

					// Font Engine (space separated list) supercedes legacy isGraphite flag
					string engines = fontElem.GetAttributeValue("engines");
					if (!String.IsNullOrEmpty(engines))
					{
						IEnumerable<string> engineList = engines.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
						foreach (string engineEntry in engineList)
						{
							fd.Engines |= (EngineToFontEngines[engineEntry]);
						}
					}

					// Subset
					fd.Subset = fontElem.GetAttributeValue("subset").ToLower();

					// URL elements
					foreach (XElement urlElem in fontElem.Elements(Sil + "url"))
					{
						fd.Urls.Add(urlElem.Value);
					}
					ws.Fonts.Add(fd);
				}
			}
		}

		private void ReadSpellcheckElement(XElement externalResourcesElem, WritingSystemDefinition ws)
		{
			foreach (XElement scElem in externalResourcesElem.Elements(Sil + "spellcheck"))
			{
				string type = scElem.GetAttributeValue("type");
				if (!type.Equals(string.Empty))
				{
					SpellCheckDictionaryDefinition scd =
						new SpellCheckDictionaryDefinition(ws.LanguageTag, SpellCheckToSpecllCheckDictionaryFormats[type]);

					// URL elements
					foreach (XElement urlElem in scElem.Elements(Sil + "url"))
					{
						scd.Urls.Add(urlElem.Value);
					}
					ws.SpellCheckDictionaries.Add(scd);
				}
			}
		}

		private void ReadKeyboardElement(XElement externalResourcesElem, WritingSystemDefinition ws)
		{
			foreach (XElement kbdElem in externalResourcesElem.Elements(Sil + "kbd"))
			{
				string id = kbdElem.GetAttributeValue("id");
				if (!string.IsNullOrEmpty(id))
				{
					KeyboardFormat format = KeyboardToKeyboardFormat[kbdElem.GetAttributeValue("type")];
					List<string> urls = new List<string>();
					foreach (XElement urlElem in kbdElem.Elements(Sil + "url"))
					{
						urls.Add(urlElem.Value);
					}
					IKeyboardDefinition keyboard = Keyboard.Controller.CreateKeyboardDefinition(id, format, urls );
					ws.KnownKeyboards.Add(keyboard);
				}
			}
		}

		private void ReadIdentityElement(XElement identityElem, WritingSystemDefinition ws)
		{
			Debug.Assert(identityElem.Name == "identity");
			XElement versionElem = identityElem.Element("version");
			if (versionElem != null)
			{
				ws.VersionNumber = (string) versionElem.Attribute("number") ?? string.Empty;
				ws.VersionDescription = (string) versionElem;
			}

			XElement generationElem = identityElem.Element("generation");
			if (generationElem != null)
			{
				string dateTime = (string) generationElem.Attribute("date") ?? string.Empty;
				DateTime modified = DateTime.UtcNow;
				const string dateUninitialized = "$Date$";
				if (!string.Equals(dateTime, dateUninitialized) && (!string.IsNullOrEmpty(dateTime.Trim()) && !DateTime.TryParse(dateTime, out modified)))
				{
					//CVS format:    "$Date: 2008/06/18 22:52:35 $"
					modified = DateTime.ParseExact(dateTime, "'$Date: 'yyyy/MM/dd HH:mm:ss $", null,
						DateTimeStyles.AssumeUniversal);
				}

				ws.DateModified = modified;
			}

			string language = identityElem.GetAttributeValue("language", "type");
			string script = identityElem.GetAttributeValue("script", "type");
			string region = identityElem.GetAttributeValue("territory", "type");
			string variant = identityElem.GetAttributeValue("variant", "type");

			if ((language.StartsWith("x-", StringComparison.OrdinalIgnoreCase) || language.Equals("x", StringComparison.OrdinalIgnoreCase)))
			{
				var flexRfcTagInterpreter = new FlexConformPrivateUseRfc5646TagInterpreter();
				flexRfcTagInterpreter.ConvertToPalasoConformPrivateUseRfc5646Tag(language, script, region, variant);
				ws.SetAllComponents(flexRfcTagInterpreter.Language, flexRfcTagInterpreter.Script, flexRfcTagInterpreter.Region, flexRfcTagInterpreter.Variant);

				_wsIsFlexPrivateUse = true;
			}
			else
			{
				ws.SetAllComponents(language, script, region, variant);

				_wsIsFlexPrivateUse = false;
			}

			//Set the id simply as the concatenation of whatever was in the ldml file.
			ws.Id = String.Join("-", new[] {language, script, region, variant}.Where(subtag => !String.IsNullOrEmpty(subtag)).ToArray());

			// TODO: Parse rest of special element.  Currently only handling a subset
			XElement specialElem = identityElem.Element("special");
			if (specialElem != null)
			{
				XElement silIdentityElem = specialElem.Element(Sil + "identity");
				if (silIdentityElem != null)
				{
					ws.WindowsLcid = silIdentityElem.GetAttributeValue("windowsLCID");
					ws.DefaultRegion = silIdentityElem.GetAttributeValue("defaultRegion");
					string variantName = silIdentityElem.GetAttributeValue("variantName");
					if (!string.IsNullOrEmpty(variantName) && ws.Variants.Count > 0)
						ws.Variants[0] = new VariantSubtag(ws.Variants[0], variantName);
				}
			}
		}

		private void ReadCharacterElement(XElement charactersElem, WritingSystemDefinition ws)
		{
			Debug.Assert(charactersElem.Name == "characters");

			foreach (XElement exemplarCharactersElem in charactersElem.Elements("exemplarCharacters"))
			{
				ReadExemplarCharactersElem(exemplarCharactersElem, ws);
			}

			XElement specialElem = charactersElem.Element("special");
			if (specialElem != null)
			{
				foreach (XElement exemplarCharactersElem in specialElem.Elements(Sil + "exemplarCharacters"))
				{
					// Sil:exemplarCharacters are required to have a type
					if (!string.IsNullOrEmpty((string)exemplarCharactersElem.Attribute("type")))
						ReadExemplarCharactersElem(exemplarCharactersElem, ws);
				}
			}
		}

		private void ReadExemplarCharactersElem(XElement exemplarCharactersElem, WritingSystemDefinition ws)
		{
			string type = (string) exemplarCharactersElem.Attribute("type") ?? "main";
			CharacterSetDefinition csd = new CharacterSetDefinition(type);

			var charList = UnicodeSet.ToCharacters((string) exemplarCharactersElem);
			foreach (string charItem in charList)
			{
				csd.Characters.Add(charItem);
			}
			ws.CharacterSets.Add(csd);
		}

		private void ReadDelimitersElement(XElement delimitersElem, WritingSystemDefinition ws)
		{
			Debug.Assert(delimitersElem.Name == "delimiters");

			// level 1: quotationStart, quotationEnd
			string open = (string)delimitersElem.Element("quotationStart");
			string close = (string)delimitersElem.Element("quotationEnd");
			if (!string.IsNullOrEmpty(open) || (!string.IsNullOrEmpty(close)))
			{
				var qm = new QuotationMark(open, close, null, 1, QuotationMarkingSystemType.Normal);
				ws.QuotationMarks.Add(qm);
			}

			// level 2: alternateQuotationStart, alternateQuotationEnd
			open = (string)delimitersElem.Element("alternateQuotationStart");
			close = (string)delimitersElem.Element("alternateQuotationEnd");
			if (!string.IsNullOrEmpty(open) || (!string.IsNullOrEmpty(close)))
			{
				var qm = new QuotationMark(open, close, null, 2, QuotationMarkingSystemType.Normal);
				ws.QuotationMarks.Add(qm);
			}

			XElement specialElem = delimitersElem.Element("special");
			if (specialElem != null)
			{
				XElement matchedPairsElem = specialElem.Element(Sil + "matched-pairs");
				if (matchedPairsElem != null)
				{
					foreach (XElement matchedPairElem in matchedPairsElem.Elements(Sil + "matched-pair"))
					{
						open = matchedPairElem.GetAttributeValue("open");
						close = matchedPairElem.GetAttributeValue("close");
						bool paraClose = (bool?) matchedPairElem.Attribute("paraClose") ?? false;
						MatchedPair mp = new MatchedPair(open, close, paraClose);
						ws.MatchedPairs.Add(mp);
					}
				}

				XElement punctuationPatternsElem = specialElem.Element(Sil + "punctuation-patterns");
				if (punctuationPatternsElem != null)
				{
					foreach (XElement punctuationPatternElem in punctuationPatternsElem.Elements(Sil + "punctuation-pattern"))
					{
						string pattern = punctuationPatternElem.GetAttributeValue("pattern");
						PunctuationPatternContext ppc = ContextToPunctuationPatternContext[
							punctuationPatternElem.GetAttributeValue("context")];
						PunctuationPattern pp = new PunctuationPattern(pattern, ppc);
						ws.PunctuationPatterns.Add(pp);
					}
				}

				XElement quotationsElem = specialElem.Element(Sil + "quotation-marks");
				if (quotationsElem != null)
				{
					ws.QuotationParagraphContinueType = QuotationToQuotationParagraphContinueTypes[
						quotationsElem.GetAttributeValue("paraContinueType")];

					foreach (XElement quotationElem in quotationsElem.Elements(Sil + "quotation"))
					{
						open = quotationElem.GetAttributeValue("open");
						close = quotationElem.GetAttributeValue("close");
						string cont = quotationElem.GetAttributeValue("continue");
						int level = (int?)quotationElem.Attribute("level") ?? 1;
						string type = quotationElem.GetAttributeValue("type");
						QuotationMarkingSystemType qmType = !string.IsNullOrEmpty(type) ? QuotationToQuotationMarkingSystemTypes[type] : QuotationMarkingSystemType.Normal;
						
						var qm = new QuotationMark(open, close, cont, level, qmType);
						ws.QuotationMarks.Add(qm);
					}
				}
			}
		}

		private void ReadLayoutElement(XElement layoutElem, WritingSystemDefinition ws)
		{
			// The orientation node has two attributes, "lines" and "characters" which define direction of writing.
			// The valid values are: "top-to-bottom", "bottom-to-top", "left-to-right", and "right-to-left"
			// Currently we only handle horizontal character orders with top-to-bottom line order, so
			// any value other than characters right-to-left, we treat as our default left-to-right order.
			// This probably works for many scripts such as various East Asian scripts which traditionally
			// are top-to-bottom characters and right-to-left lines, but can also be written with
			// left-to-right characters and top-to-bottom lines.
			//Debug.Assert(layoutElem.NodeType == XmlNodeType.Element && layoutElem.Name == "layout");
			string characterOrder = layoutElem.GetAttributeValue("orientation", "characterOrder");
			ws.RightToLeftScript = (characterOrder == "right-to-left");
		}

		// Numbering system gets added to the character set definition
		private void ReadNumbersElement(XElement numbersElem, WritingSystemDefinition ws)
		{
			Debug.Assert(numbersElem.Name == "numbers");

			XElement defaultNumberingSystemElem = numbersElem.Element("defaultNumberingSystem");
			if (defaultNumberingSystemElem != null)
			{
				string id = (string) defaultNumberingSystemElem;
				var numberingSystemsElem =
					numbersElem.Elements("numberingSystem")
						.Where(e => id == e.GetAttributeValue("id") && e.GetAttributeValue("type") == "numeric")
						.FirstOrDefault();
				if (numberingSystemsElem != null)
				{
					var csd = new CharacterSetDefinition("numeric");
					// Only handle numeric types
					string digits = numberingSystemsElem.GetAttributeValue("digits");
					foreach (var charItem in digits)
					{
						csd.Characters.Add(charItem.ToString());
					}
					ws.CharacterSets.Add(csd);
				}
			}
		}

		private void ReadCollationsElement(XElement collationsElem, WritingSystemDefinition ws)
		{
			Debug.Assert(collationsElem.Name == "collations");
			ws.Collations.Clear();
			XElement defaultCollationElem = collationsElem.Element("defaultCollation");
			string defaultCollation = (string) defaultCollationElem ?? "standard";
			foreach (XElement collationElem in collationsElem.Elements("collation"))
				ReadCollationElement(collationElem, ws, defaultCollation);
		}

		private void ReadCollationElement(XElement collationElem, WritingSystemDefinition ws, string defaultCollation)
		{
			Debug.Assert(collationElem != null);
			Debug.Assert(ws != null);

			string collationType = collationElem.GetAttributeValue("type");
			bool needsCompiling = (bool?) collationElem.Attribute(Sil + "needscompiling") ?? false;

			CollationDefinition cd = null;
			XElement specialElem = collationElem.Element("special");
			if ((specialElem != null) && (specialElem.HasElements))
			{
				string specialType = (specialElem.Elements().First().Name.LocalName);
				switch (specialType)
				{
					case "inherited":
						XElement inheritedElem = specialElem.Element(Sil + "inherited");
						cd = ReadCollationRulesForOtherLanguage(inheritedElem, collationType);
						break;
					case "simple":
						XElement simpleElem = specialElem.Element(Sil + "simple");
						cd = ReadCollationRulesForCustomSimple(simpleElem, collationType);
						break;
					case "reordered":
						// Skip for now
						break;
				}
			}
			else
			{
				cd = new CollationDefinition(collationType);
			}

			// Only add collation definition if it's been set
			if (cd != null)
			{
				// If ICU rules are out of sync, re-compile
				if (needsCompiling)
				{
					string errorMsg;
					cd.Validate(out errorMsg);
					// TODO: Throw exception with ErrorMsg?
				}
				else
				{
					cd.IcuRules = LdmlCollationParser.GetIcuRulesFromCollationNode(collationElem);
					cd.IsValid = true;
				}

				ws.Collations.Add(cd);
				if (collationType == defaultCollation)
					ws.DefaultCollation = cd;
			}
		}

		private CollationDefinition ReadCollationRulesForOtherLanguage(XElement inheritedElem, string collationType)
		{
			Debug.Assert(inheritedElem != null);
			string baseLanguageTag = inheritedElem.GetAttributeValue("base");
			string baseType = inheritedElem.GetAttributeValue("type");

			// TODO: Read referenced LDML and get collation from there
			return new InheritedCollationDefinition(collationType) {BaseLanguageTag = baseLanguageTag, BaseType = baseType};
		}

		private CollationDefinition ReadCollationRulesForCustomSimple(XElement simpleElem, string collationType)
		{
			Debug.Assert(simpleElem != null);

			return new SimpleCollationDefinition(collationType) {SimpleRules = (string) simpleElem};
		}

		/// <summary>
		/// The "oldFile" parameter allows the LdmldataMapper to allow data that it doesn't understand to be roundtripped.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="ws"></param>
		/// <param name="oldFile"></param>
		public void Write(string filePath, WritingSystemDefinition ws, Stream oldFile)
		{
#if WS_FIX
			_compatibilityMode = compatibilityMode;
#endif
			if (filePath == null)
			{
				throw new ArgumentNullException("filePath");
			}
			if (ws == null)
			{
				throw new ArgumentNullException("ws");
			}
			XmlReader reader = null;
			try
			{
				XElement element = null;
				if (oldFile != null)
				{
					var readerSettings = new XmlReaderSettings
					{
						IgnoreWhitespace = true,
						ConformanceLevel = ConformanceLevel.Auto,
						ValidationType = ValidationType.None,
						XmlResolver = null,
						DtdProcessing = DtdProcessing.Parse
					};
					reader = XmlReader.Create(oldFile, readerSettings);
					element = XElement.Load(reader);
				}
				else
				{
					element = new XElement("ldml");
				}
				// Use Canonical xml settings suitable for use in Chorus applications
				// except NewLineOnAttributes to conform to SLDR files
				var writerSettings = CanonicalXmlSettings.CreateXmlWriterSettings();
				writerSettings.NewLineOnAttributes = false;
				using (var writer = XmlWriter.Create(filePath, writerSettings))
				{
					// Assign SIL namespace
					element.SetAttributeValue(XNamespace.Xmlns + "sil", Sil);
					WriteLdml(writer, element, ws);
					writer.Close();
				}
			}
			finally
			{
				if (reader != null)
				{
					reader.Close();
				}
			}
		}

		/// <summary>
		/// The "oldFileReader" parameter allows the LdmldataMapper to allow data that it doesn't understand to be roundtripped.
		/// </summary>
		/// <param name="xmlWriter"></param>
		/// <param name="ws"></param>
		/// <param name="oldFileReader"></param>
		public void Write(XmlWriter xmlWriter, WritingSystemDefinition ws, XmlReader oldFileReader)
		{
#if WS_FIX
			_compatibilityMode = compatibilityMode;
#endif
			if (xmlWriter == null)
			{
				throw new ArgumentNullException("xmlWriter");
			}
			if (ws == null)
			{
				throw new ArgumentNullException("ws");
			}
			try
			{
				XElement element = null;
				if (oldFileReader != null)
				{
					element = XElement.Load(oldFileReader);
				}
				else
				{
					element = new XElement("ldml");
				}
				WriteLdml(xmlWriter, element, ws);
			}
			finally
			{
			}
		}

		/// <summary>
		/// Update element based on the writing system model.  At the end, write the contents to LDML
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="element"></param>
		/// <param name="ws"></param>
		private void WriteLdml(XmlWriter writer, XElement element, WritingSystemDefinition ws)
		{
			_wsIsFlexPrivateUse = false;
			Debug.Assert(element != null);
			Debug.Assert(ws != null);

			XElement identityElem = element.GetOrCreateElement("identity");
			WriteIdentityElement(identityElem, ws);
			if (identityElem.IsEmpty)
				identityElem.Remove();

			XElement charactersElem = element.GetOrCreateElement("characters");
			WriteCharactersElement(charactersElem, ws);
			if (charactersElem.IsEmpty)
				charactersElem.Remove();

			XElement delimitersElem = element.GetOrCreateElement("delimiters");
			WriteDelimitersElement(delimitersElem, ws);
			if (delimitersElem.IsEmpty)
				delimitersElem.Remove();

			XElement layoutElem = element.GetOrCreateElement("layout");
			WriteLayoutElement(layoutElem, ws);
			if (layoutElem.IsEmpty)
				layoutElem.Remove();

			XElement numbersElem = element.GetOrCreateElement("numbers");
			WriteNumbersElement(numbersElem, ws);
			if (numbersElem.IsEmpty)
				numbersElem.Remove();

			XElement collationsElem = element.GetOrCreateElement("collations");
			WriteCollationsElement(collationsElem, ws);
			if (collationsElem.IsEmpty)
				collationsElem.Remove();

			// TODO: Can have multiple specials.  Find the one with external-resources.  Also handle case where we
			// create special because writingsystem has entries to write
			XElement specialElem = element.Elements("special").FirstOrDefault(e => e.Element(Sil + "external-resources") != null);
			if (specialElem == null && (ws.Fonts.Count > 0 || ws.KnownKeyboards.Count > 0 || ws.SpellCheckDictionaries.Count > 0))
			{
				// Create element
				specialElem = element.GetOrCreateElement("special");
			}
			if (specialElem != null)
			{
				WriteTopLevelSpecialElements(specialElem, ws);
				if (specialElem.IsEmpty)
					specialElem.Remove();
			}

			element.WriteTo(writer);
		}

		private void WriteIdentityElement(XElement identityElem, WritingSystemDefinition ws)
		{
			Debug.Assert(identityElem != null);
			Debug.Assert(ws != null);

			// Remove non-special elements and repopulate later
			identityElem.Elements().Where(e => e.Name != "special").Remove();

			// Remove special sil:identity elements and repopulate later
			XElement specialElem = identityElem.Element("special");
			if (specialElem != null)
			{
				identityElem.Element("special").Elements().Where(e => e.Name == Sil + "identity").Remove();
				if (specialElem.IsEmpty)
					specialElem.Remove();
			}

			// Version is required.  If VersionNumber is blank, the empty attribute is still written
			XElement versionElem = identityElem.GetOrCreateElement("version");
			versionElem.SetAttributeValue("number", ws.VersionNumber);

			identityElem.SetAttributeValue("generation", "date", String.Format("{0:s}", ws.DateModified));
			// TODO: Keeping this block until we sort out migration
#if WS_FIX
			bool copyFlexFormat = false;
			string language = String.Empty;
			string script = String.Empty;
			string territory = String.Empty;
			string variant = String.Empty;
			bool readerIsOnIdentityElement = IsReaderOnElementNodeNamed(reader, "identity");
			if (readerIsOnIdentityElement && !reader.IsEmptyElement)
			{
				reader.ReadToDescendant("language");
				while(!IsReaderOnElementNodeNamed(reader, "special") && !IsReaderOnEndElementNodeNamed(reader, "identity"))
				{
					switch(reader.Name)
					{
						case "language":
							language = reader.GetAttribute("type");
							break;
						case "script":
							script = reader.GetAttribute("type");
							break;
						case "territory":
							territory = reader.GetAttribute("type");
							break;
						case "variant":
							variant = reader.GetAttribute("type");
							break;
					}
					reader.Read();
				}
				if (_compatibilityMode == WritingSystemCompatibility.Flex7V0Compatible)
				{
					var interpreter = new FlexConformPrivateUseRfc5646TagInterpreter();
					interpreter.ConvertToPalasoConformPrivateUseRfc5646Tag(language, script, territory, variant);
					if ((language.StartsWith("x-", StringComparison.OrdinalIgnoreCase) ||  language.Equals("x", StringComparison.OrdinalIgnoreCase))&&
						interpreter.Rfc5646Tag == ws.Bcp47Tag)
					{
						copyFlexFormat = true;
						_wsIsFlexPrivateUse = true;
					}
				}
			}
			if (copyFlexFormat)
			{
				WriteRFC5646TagElements(writer, language, script, territory, variant);
			}
			else
			{
				WriteRFC5646TagElements(writer, ws.Language, ws.Script, ws.Region, ws.Variant);
			}
#else
			WriteLanguageTagElements(identityElem, ws.LanguageTag);
#endif
			// Create special element if data needs to be written
			if (!string.IsNullOrEmpty(ws.WindowsLcid) || !string.IsNullOrEmpty(ws.DefaultRegion) || (ws.Variants.Count > 0))
			{
				specialElem = identityElem.GetOrCreateElement("special");
				XElement silIdentityElem = specialElem.GetOrCreateElement(Sil + "identity");

				// TODO: how do we recover uid attribute?

				silIdentityElem.SetOptionalAttributeValue("windowsLCID", ws.WindowsLcid);
				silIdentityElem.SetOptionalAttributeValue("defaultRegion", ws.DefaultRegion);
				// TODO: For now, use the first variant as the variantName
				if (ws.Variants.Count > 0)
					silIdentityElem.SetOptionalAttributeValue("variantName", ws.Variants.First().Name);
			}
		}

		private void WriteLanguageTagElements(XElement identityElem, string languageTag) 
		{
			string language, script, region, variant;
			IetfLanguageTag.GetParts(languageTag, out language, out script, out region, out variant);
			
			// language element is required
			identityElem.SetAttributeValue("language", "type", language);
			// write the rest if they have contents
			if (!string.IsNullOrEmpty(script))
				identityElem.SetAttributeValue("script", "type", script);
			if (!string.IsNullOrEmpty(region))
				identityElem.SetAttributeValue("territory", "type", region);
			if (!string.IsNullOrEmpty(variant))
				identityElem.SetAttributeValue("variant", "type", variant);
		}
				
		private void WriteCharactersElement(XElement charactersElem, WritingSystemDefinition ws)
		{
			Debug.Assert(charactersElem != null);
			Debug.Assert(ws != null);

			// Remove all exemplarCharacters and Sil:exemplarCharacters to repopulate later
			charactersElem.Elements("exemplarCharacters").Remove();
			XElement specialElem = charactersElem.Element("special");
			if (specialElem != null)
			{
				specialElem.Elements(Sil + "exemplarCharacters").Remove();
				if (specialElem.IsEmpty)
					specialElem.Remove();
			}

			foreach (var csd in ws.CharacterSets)
			{
				XElement exemplarCharactersElem = null;
				switch (csd.Type)
				{
					// These character sets go to the normal LDML exemplarCharacters space
					// http://unicode.org/reports/tr35/tr35-general.html#Exemplars
					case "main" :
					case "auxiliary" :
					case "index" :
					case "punctuation" :
						exemplarCharactersElem = new XElement("exemplarCharacters", UnicodeSet.ToPattern(csd.Characters));
						// Assume main set doesn't have an attribute type
						if (csd.Type != "main")
							exemplarCharactersElem.SetAttributeValue("type", csd.Type);
						charactersElem.Add(exemplarCharactersElem);
						break;
					// All others go to special Sil:exemplarCharacters
					default :
						exemplarCharactersElem = new XElement(Sil + "exemplarCharacters", UnicodeSet.ToPattern(csd.Characters));
						exemplarCharactersElem.SetAttributeValue("type", csd.Type);
						specialElem = charactersElem.GetOrCreateElement("special");
						specialElem.Add(exemplarCharactersElem);
						break;
				}
			}
		}

		private void WriteDelimitersElement(XElement delimitersElem, WritingSystemDefinition ws)
		{
			Debug.Assert(delimitersElem != null);
			Debug.Assert(ws != null);

			// Remove existing non-special elements and repopulate
			delimitersElem.Elements().Where(e => e.Name != "special").Remove();

			// Level 1 normal => quotationStart and quotationEnd
			QuotationMark qm1 = ws.QuotationMarks.Where(q => q.Level == 1 && q.Type == QuotationMarkingSystemType.Normal).FirstOrDefault();
			if (qm1 != null)
			{
				var quotationStartElem = new XElement("quotationStart", qm1.Open);
				var quotationEndElem = new XElement("quotationEnd", qm1.Close);
				delimitersElem.Add(quotationStartElem);
				delimitersElem.Add(quotationEndElem);
			}
			// Level 2 normal => alternateQuotationStart and alternateQuotationEnd
			QuotationMark qm2 = ws.QuotationMarks.Where(q => q.Level == 2 && q.Type == QuotationMarkingSystemType.Normal).FirstOrDefault();
			if (qm2 != null)
			{
				var alternateQuotationStartElem = new XElement("alternateQuotationStart", qm2.Open);
				var alternateQuotationEndElem = new XElement("alternateQuotationEnd", qm2.Close);
				delimitersElem.Add(alternateQuotationStartElem);
				delimitersElem.Add(alternateQuotationEndElem);
			}

			// Remove all exisiting Sil:matched pairs and repopulate
			XElement specialElem = delimitersElem.Element("special");
			XElement matchedPairsElem = null;
			if (specialElem != null)
			{
				matchedPairsElem = specialElem.Element(Sil + "matched-pairs");
				if (matchedPairsElem != null)
				{
					matchedPairsElem.Elements(Sil + "matched-pair").Remove();
					if (matchedPairsElem.IsEmpty)
						matchedPairsElem.Remove();
				}
				if (specialElem.IsEmpty)
					specialElem.Remove();
			}
			foreach (var mp in ws.MatchedPairs)
			{
				var matchedPairElem = new XElement(Sil + "matched-pair");
				// open and close are required
				matchedPairElem.SetAttributeValue("open", mp.Open);
				matchedPairElem.SetAttributeValue("close", mp.Close);
				matchedPairElem.SetAttributeValue("paraClose", mp.ParagraphClose ); // optional, default to false?
				specialElem = delimitersElem.GetOrCreateElement("special");
				matchedPairsElem = specialElem.GetOrCreateElement(Sil + "matched-pairs");
				matchedPairsElem.Add(matchedPairElem);
			}

			// Remove all existing Sil:punctuation-patterns and repopulate
			XElement punctuationPatternsElem = null;
			if (specialElem != null)
			{
				punctuationPatternsElem = specialElem.Element(Sil + "punctuation-patterns");
				if (punctuationPatternsElem != null)
				{
					punctuationPatternsElem.Elements(Sil + "punctuation-patterns").Remove();
					if (punctuationPatternsElem.IsEmpty)
						punctuationPatternsElem.Remove();
				}
				if (specialElem.IsEmpty)
					specialElem.Remove();
			}
			foreach (var pp in ws.PunctuationPatterns)
			{
				var punctuationPatternElem = new XElement(Sil + "punctuation-pattern");
				// text is required
				punctuationPatternElem.SetAttributeValue("pattern", pp.Pattern);
				punctuationPatternElem.SetAttributeValue("context", PunctuationPatternContextToContext[pp.Context]);
				specialElem = delimitersElem.GetOrCreateElement("special");
				punctuationPatternsElem = specialElem.GetOrCreateElement(Sil + "punctuation-patterns");
				punctuationPatternsElem.Add(punctuationPatternElem);
			}

			// Preserve existing Sil:quotation-marks that aren't narrative or blank.
			// Remove the rest since we will repopulate them
			XElement quotationmarksElem = null;
			if (specialElem != null)
			{
				quotationmarksElem = specialElem.Element(Sil + "quotation-marks");
				if (quotationmarksElem != null)
				{
					quotationmarksElem.Elements(Sil + "quotation").Where(e=>string.IsNullOrEmpty(e.GetAttributeValue("type"))).Remove();
					quotationmarksElem.Elements(Sil + "quotation").Where(e=>e.GetAttributeValue("type") == "narrative").Remove();
					if (quotationmarksElem.IsEmpty)
						quotationmarksElem.Remove();
				}
				if (specialElem.IsEmpty)
					specialElem.Remove();
			}

			foreach (var qm in ws.QuotationMarks)
			{
				// Level 1 and 2 normal have already been written
				if (!((qm.Level == 1 || qm.Level == 2) && qm.Type == QuotationMarkingSystemType.Normal))
				{
					var quotationElem = new XElement(Sil + "quotation");
					// open and level required
					quotationElem.SetAttributeValue("open", qm.Open);
					quotationElem.SetOptionalAttributeValue("close", qm.Close);
					quotationElem.SetOptionalAttributeValue("continue", qm.Continue);
					quotationElem.SetAttributeValue("level", qm.Level);
					// normal quotation mark can have no attribute defined.  Narrative --> "narrative"
					quotationElem.SetAttributeValue("type", QuotationMarkingSystemTypesToQuotation[qm.Type]);

					specialElem = delimitersElem.GetOrCreateElement("special");
					quotationmarksElem = specialElem.GetOrCreateElement(Sil + "quotation-marks");
					quotationmarksElem.Add(quotationElem);
				}
			}
			if ((ws.QuotationParagraphContinueType != QuotationParagraphContinueType.None) && (quotationmarksElem != null))
			{
				quotationmarksElem.SetAttributeValue("paraContinueType",
					QuotationParagraphContinueTypesToQuotation[ws.QuotationParagraphContinueType]);
			}
		}

		private void WriteLayoutElement(XElement layoutElem, WritingSystemDefinition ws)
		{
			Debug.Assert(layoutElem != null);
			Debug.Assert(ws != null);

			// Remove characterOrder element and repopulate
			XElement orientationElem = layoutElem.Element("orientation");
			if (orientationElem != null)
			{
				orientationElem.Elements().Where(e => e.Name == "characterOrder").Remove();
				if (orientationElem.IsEmpty)
					orientationElem.Remove();
			}

			// we generally don't need to write out default values, but SLDR seems to always write characterOrder
			orientationElem = layoutElem.GetOrCreateElement("orientation");
			XElement characterOrderElem = orientationElem.GetOrCreateElement("characterOrder");
			characterOrderElem.SetValue(ws.RightToLeftScript ? "right-to-left" : "left-to-right");
			// Ignore lineOrder
		}

		private void WriteNumbersElement(XElement numbersElem, WritingSystemDefinition ws)
		{
			Debug.Assert(numbersElem != null);
			Debug.Assert(ws != null);

			// Remove numberingSystems of type numeric and repopulate
			numbersElem.Elements("numberingSystem").Where(e => e.GetAttributeValue("type") == "numeric").Remove();
 
			foreach (var csd in ws.CharacterSets)
			{
				if (csd.Type == "numeric")
				{
					var numberingSystemsElem = new XElement("numberingSystems");
					numberingSystemsElem.SetAttributeValue("type", csd.Type);
					string digits = string.Join("", csd.Characters);
					numberingSystemsElem.SetAttributeValue("digits", digits);
					numbersElem.Add(numberingSystemsElem);
				}
			}
		}

		private void WriteCollationsElement(XElement collationsElem, WritingSystemDefinition ws)
		{
			Debug.Assert(collationsElem != null);
			Debug.Assert(ws != null);

			// Preserve exisiting collations since we don't process them all
			// Remove only the collations we can repopulate from the writing system
			collationsElem.Descendants("special").Where(e => e.Name != (Sil + "reordered")).Remove();
			collationsElem.Descendants("special").Where(e => e.IsEmpty).Remove();

			if (ws.DefaultCollation != null)
			{
				XElement defaultCollationElem = collationsElem.GetOrCreateElement("defaultCollation");
				defaultCollationElem.SetValue(ws.DefaultCollation.Type);
			}
			
			foreach (var collation in ws.Collations)
			{
				WriteCollationElement(collationsElem, collation);
			}
		}

		private void WriteCollationElement(XElement collationsElem, CollationDefinition collation)
		{
			Debug.Assert(collationsElem != null);
			Debug.Assert(collation != null);

			// Find the collation with the matching attribute Type
			var collationElem = collationsElem.Elements("collation").Where(e=> e.GetAttributeValue("type") == collation.Type).FirstOrDefault();
			if (collationElem == null)
			{
				collationElem = new XElement("collation", new XAttribute("type", collation.Type));
				collationsElem.Add(collationElem);
			}
			// If collation valid and icu rules exist, populate icu rules
			if (!string.IsNullOrEmpty(collation.IcuRules))
			{
				XElement crElem = collationElem.GetOrCreateElement("cr");
				// Remove existing Icu rule
				crElem.RemoveAll();
				crElem.Add(new XCData(collation.IcuRules));
				// SLDR generally doesn't include needsCompiling if false
				if (collation.IsValid)
					collationElem.SetAttributeValue(Sil + "needsCompiling", null);
				else
					collationElem.SetAttributeValue(Sil + "needsCompiling", "true");
			}
			var inheritedCollation = collation as InheritedCollationDefinition;
			if (inheritedCollation != null)
			{
				XElement specialElem = collationElem.GetOrCreateElement("special");
				collationElem = specialElem.GetOrCreateElement(Sil + "inherited");
				WriteCollationRulesFromOtherLanguage(collationElem, (InheritedCollationDefinition)collation);
			}
			var simpleCollation = collation as SimpleCollationDefinition;
			if (simpleCollation != null)
			{
				XElement specialElem = collationElem.GetOrCreateElement("special");
				collationElem = specialElem.GetOrCreateElement(Sil + "simple");
				WriteCollationRulesFromCustomSimple(collationElem, (SimpleCollationDefinition)collation);
			}
			
		}

		private void WriteCollationRulesFromOtherLanguage(XElement collationElement, InheritedCollationDefinition cd)
		{
			Debug.Assert(collationElement != null);
			Debug.Assert(cd != null);
			
			// base and type are required attributes
			collationElement.SetAttributeValue("base", cd.BaseLanguageTag);
			collationElement.SetAttributeValue("type", cd.BaseType);
		}

		private void WriteCollationRulesFromCustomSimple(XElement collationElement, SimpleCollationDefinition cd)
		{
			Debug.Assert(collationElement != null);
			Debug.Assert(cd != null);

			collationElement.Add(new XCData(cd.SimpleRules));
		}
		
		private void WriteTopLevelSpecialElements(XElement specialElem, WritingSystemDefinition ws)
		{
			XElement externalResourcesElem = specialElem.GetOrCreateElement(Sil + "external-resources");
			WriteFontElement(externalResourcesElem, ws);
			WriteSpellcheckElement(externalResourcesElem, ws);
			WriteKeyboardElement(externalResourcesElem, ws);
		}

		private void WriteFontElement(XElement externalResourcesElem, WritingSystemDefinition ws)
		{
			Debug.Assert(externalResourcesElem != null);
			Debug.Assert(ws != null);

			// Remove exisiting fonts and repopulate
			externalResourcesElem.Elements(Sil + "font").Remove();
			foreach (var font in ws.Fonts)
			{
				var fontElem = new XElement(Sil + "font");
				fontElem.SetAttributeValue("name", font.Name);

				// Generate space-separated list of font roles
				if (font.Roles != FontRoles.Default)
				{
					List<string> fontRoleList = new List<string>();
					foreach (FontRoles fontRole in Enum.GetValues(typeof(FontRoles)))
					{
						if ((font.Roles & fontRole) != 0)
							fontRoleList.Add(FontRolesToRole[fontRole]);
					}
					fontElem.SetAttributeValue("types", string.Join(" ", fontRoleList));
				}

				if (font.DefaultRelativeSize != 1.0f)
				{
					fontElem.SetAttributeValue("size", font.DefaultRelativeSize);
				}

				fontElem.SetOptionalAttributeValue("minverison", font.MinVersion);
				fontElem.SetOptionalAttributeValue("features", font.Features);
				fontElem.SetOptionalAttributeValue("lang", font.Language);
				fontElem.SetOptionalAttributeValue("otlang", font.OpenTypeLanguage);
				fontElem.SetOptionalAttributeValue("subset", font.Subset);

				// Generate space-separated list of font engines
				if (font.Engines != (FontEngines.Graphite | FontEngines.OpenType))
				{
					List<string> fontEngineList = new List<string>();
					foreach (FontEngines fontEngine in Enum.GetValues(typeof (FontEngines)))
					{
						if ((font.Engines & fontEngine) != 0)
							fontEngineList.Add(FontEnginesToEngine[fontEngine]);
					}
					fontElem.SetAttributeValue("engines", string.Join(" ", fontEngineList));
				}

				externalResourcesElem.Add(fontElem);
			}
		}

		private void WriteSpellcheckElement(XElement externalResourcesElem, WritingSystemDefinition ws)
		{
			Debug.Assert(externalResourcesElem != null);
			Debug.Assert(ws != null);

			// Remove spellcheck entries and repopulate
			externalResourcesElem.Elements(Sil + "spellcheck").Remove();
			foreach (SpellCheckDictionaryDefinition scd in ws.SpellCheckDictionaries)
			{
				var scElem = new XElement(Sil + "spellcheck");
				scElem.SetAttributeValue("type", SpellCheckDictionaryFormatsToSpellCheck[scd.Format]);

				// URL elements
				foreach (var url in scd.Urls)
				{
					var urlElem  = new XElement(Sil + "url", url);
					scElem.Add(urlElem);
				}
				externalResourcesElem.Add(scElem);
			}
		}

		private void WriteKeyboardElement(XElement externalResourcesElem, WritingSystemDefinition ws)
		{
			Debug.Assert(externalResourcesElem != null);
			Debug.Assert(ws != null);
			
			// Remove keyboard entries and repopulate
			externalResourcesElem.Elements(Sil + "keyboard").Remove();

			foreach (var keyboard in ws.KnownKeyboards)
			{
				var kbdElem = new XElement(Sil + "kbd");
				// id required
				kbdElem.SetAttributeValue("id", keyboard.Id);
				if (!string.IsNullOrEmpty(keyboard.Id))
				{
					kbdElem.SetAttributeValue("type", KeyboardFormatToKeyboard[keyboard.Format]);
					foreach (var url in keyboard.Urls)
					{
						var urlElem = new XElement(Sil + "url", url);
						kbdElem.Add(urlElem);
					}
				}
				externalResourcesElem.Add(kbdElem);
			}
		}
	}
}