// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.Serialization;

namespace Microsoft.UsEduCsu.Saas;

[Serializable]
internal class MissingConfigurationException : Exception
{
	public MissingConfigurationException()
	{
	}

	public MissingConfigurationException(string message) : base(message)
	{
	}

	public MissingConfigurationException(string message, Exception innerException) : base(message, innerException)
	{
	}

	protected MissingConfigurationException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}