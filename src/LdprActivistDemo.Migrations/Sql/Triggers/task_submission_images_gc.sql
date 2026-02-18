CREATE OR REPLACE FUNCTION task_submission_images_gc_images()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
	BEGIN
		DELETE FROM images i
		WHERE i.id = OLD.image_id
			AND NOT EXISTS (
				SELECT 1
				FROM task_submission_images tsi
				WHERE tsi.image_id = OLD.image_id
			);
	EXCEPTION
		WHEN foreign_key_violation THEN
			NULL;
	END;

	RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_task_submission_images_gc_images ON task_submission_images;

CREATE TRIGGER trg_task_submission_images_gc_images
AFTER DELETE ON task_submission_images
FOR EACH ROW
EXECUTE FUNCTION task_submission_images_gc_images();