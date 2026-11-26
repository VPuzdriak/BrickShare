using System;
using System.Collections.Generic;

namespace BrickShare.Catalog.Api.Features.Instances.LabelSheet;

public sealed record LabelSheetRequest(IReadOnlyList<Guid> InstanceIds);