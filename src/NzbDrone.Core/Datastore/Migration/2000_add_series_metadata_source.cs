using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration;

// Fork-specific migration: numbered far above upstream migrations so future upstream
// migrations do not collide with it (FluentMigrator applies any unapplied migration).
[Migration(2000)]
public class add_series_metadata_source : NzbDroneMigrationBase
{
    protected override void MainDbUpgrade()
    {
        Alter.Table("Series").AddColumn("MetadataSource").AsInt32().WithDefaultValue(0);
    }
}
