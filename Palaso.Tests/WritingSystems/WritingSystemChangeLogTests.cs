﻿using NUnit.Framework;
using Palaso.WritingSystems;

namespace Palaso.Tests.WritingSystems
{
	[TestFixture]
	public class WritingSystemChangeLogTests
	{
		[Test]
		public void HasChangeFor_InLogHasChange_ReturnsTrue()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aab", "bba", "WeSay", "1.1");
			Assert.That(log.HasChangeFor("aab"), Is.True);
		}

		[Test]
		public void HasChangeFor_InLogNoChange_ReturnsFalse()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aaa", "bbb", "WeSay", "1.1");
			log.Set("bbb", "aaa", "WeSay", "1.1");
			Assert.That(log.HasChangeFor("aaa"), Is.False);
		}

		[Test]
		public void HasChangeFor_NotInLog_ReturnsFalse()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aab", "bba", "WeSay", "1.1");
			Assert.That(log.HasChangeFor("fff"), Is.False);
		}

		[Test]
		public void GetChangeFor_HasChange_ReturnsCorrectWsId()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aab", "bba", "WeSay", "1.1");
			Assert.That(log.GetChangeFor("aab"), Is.EqualTo("bba"));
		}

		[Test]
		public void GetChangeFor_NotInLog_ReturnsNull()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aab", "bba", "WeSay", "1.1");
			Assert.That(log.GetChangeFor("fff"), Is.Null);
		}

		[Test]
		public void GetChangeFor_InLogButNoChange_ReturnsNull()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aaa", "bbb", "WeSay", "1.1");
			log.Set("bbb", "aaa", "WeSay", "1.1");
			Assert.That(log.GetChangeFor("aaa"), Is.Null);
		}

		[Test]
		public void Set_NormalData_SetsCorrectly()
		{
			var log = new WritingSystemChangeLog();
			log.Set("aab", "bba", "WeSay", "1.1");
			var changes = log.Items;
			Assert.That(changes.Count, Is.EqualTo(1));
			WritingSystemChange change = changes[0];
			Assert.That(change.From, Is.EqualTo("aab"));
		}
	}
}