using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	public partial class AddImagesGcTriggers : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// 1) GC при удалении связи submission <-> image
			migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION task_submission_images_gc_images()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	DELETE FROM images i
	WHERE i.""Id"" = OLD.""ImageId""
		AND NOT EXISTS (
			SELECT 1
			FROM task_submission_images tsi
			WHERE tsi.""ImageId"" = OLD.""ImageId""
		)
		AND NOT EXISTS (
			SELECT 1
			FROM tasks t
			WHERE t.""CoverImageId"" = OLD.""ImageId""
		);

	RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_task_submission_images_gc_images ON task_submission_images;

CREATE TRIGGER trg_task_submission_images_gc_images
AFTER DELETE ON task_submission_images
FOR EACH ROW
EXECUTE FUNCTION task_submission_images_gc_images();
");

			// 2) GC при удалении задачи или смене CoverImageId
			migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION tasks_gc_cover_image()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
	old_image_id uuid;
BEGIN
	old_image_id := OLD.""CoverImageId"";

	IF old_image_id IS NULL THEN
		RETURN NULL;
	END IF;

	IF TG_OP = 'UPDATE' AND NEW.""CoverImageId"" IS NOT DISTINCT FROM old_image_id THEN
		RETURN NULL;
	END IF;

	DELETE FROM images i
	WHERE i.""Id"" = old_image_id
		AND NOT EXISTS (
			SELECT 1 FROM tasks t WHERE t.""CoverImageId"" = old_image_id
		)
		AND NOT EXISTS (
			SELECT 1 FROM task_submission_images tsi WHERE tsi.""ImageId"" = old_image_id
		);

	RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_tasks_gc_cover_image ON tasks;

CREATE TRIGGER trg_tasks_gc_cover_image
AFTER DELETE OR UPDATE OF ""CoverImageId"" ON tasks
FOR EACH ROW
EXECUTE FUNCTION tasks_gc_cover_image();
");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_task_submission_images_gc_images ON task_submission_images;
DROP FUNCTION IF EXISTS task_submission_images_gc_images();

DROP TRIGGER IF EXISTS trg_tasks_gc_cover_image ON tasks;
DROP FUNCTION IF EXISTS tasks_gc_cover_image();
");
		}
	}
}
