import { useRef, useState } from "react";
import ConfirmSheet from "./ConfirmSheet";
import UserAvatar from "./UserAvatar";

const PHOTO_HINT = "JPEG, PNG, WebP, or GIF. 2 MB or smaller.";

export default function PhotoEditor({
  userId,
  name,
  email,
  hasPhoto,
  revision,
  error,
  onUpload,
  onClear
}: {
  userId?: string;
  name?: string | null;
  email?: string | null;
  hasPhoto: boolean;
  revision: number;
  error?: string | null;
  onUpload: (file: File) => Promise<void>;
  onClear: () => Promise<void>;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [pendingClear, setPendingClear] = useState(false);
  const [busy, setBusy] = useState(false);

  async function pick(file: File | undefined) {
    if (!file) {
      return;
    }
    setBusy(true);
    try {
      await onUpload(file);
    } finally {
      setBusy(false);
      if (inputRef.current) {
        inputRef.current.value = "";
      }
    }
  }

  return (
    <div className="photo-editor">
      <UserAvatar userId={userId} name={name} email={email} hasPhoto={hasPhoto} revision={revision} />
      <div className="photo-editor-actions">
        <input
          ref={inputRef}
          className="visually-hidden"
          type="file"
          accept="image/jpeg,image/png,image/webp,image/gif"
          onChange={(e) => void pick(e.target.files?.[0])}
        />
        <button className="ghost" type="button" disabled={busy} onClick={() => inputRef.current?.click()}>
          {hasPhoto ? "Replace Photo" : "Upload Photo"}
        </button>
        {hasPhoto && (
          <button className="ghost" type="button" disabled={busy} onClick={() => setPendingClear(true)}>
            Remove Photo
          </button>
        )}
        <span className="field-hint">{PHOTO_HINT}</span>
        {error && (
          <span className="field-error" role="alert">
            {error}
          </span>
        )}
      </div>
      {pendingClear && (
        <ConfirmSheet
          title="Remove this photo?"
          body="The profile photo will be cleared. A default initials avatar will show until a new photo is uploaded."
          confirmLabel="Remove Photo"
          onCancel={() => setPendingClear(false)}
          onConfirm={() => {
            setPendingClear(false);
            void onClear();
          }}
        />
      )}
    </div>
  );
}
