﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Palaso.DictionaryServices.Model;
using Palaso.Lift;
using Palaso.Tests.Code;

namespace Palaso.DictionaryServices.Tests.Model
{
	[TestFixture]
	public class LexFieldIClonableGenericTests : IClonableGenericTests<IPalasoDataObjectProperty>
	{
		public override IPalasoDataObjectProperty CreateNewClonable()
		{
			return new LexField("type");
		}

		public override string ExceptionList
		{
			get { return ""; }
		}

		public override Dictionary<Type, object> DefaultValuesForTypes
		{
			get { return new Dictionary<Type, object>(); }
		}
	}

	[TestFixture]
	class LexFieldTests
	{
	}
}