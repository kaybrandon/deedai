import { FormEvent, useState } from "react";
import { endpoints, type ApiError } from "../api";
import { useAuth } from "../auth";
import PasswordPair, { passwordPairErrors } from "../components/PasswordPair";
import PhotoEditor from "../components/PhotoEditor";

export default function ProfilePage() {
  const { me, refreshMe } = useAuth();
  const [displayName, setDisplayName] = useState(me?.displayName ?? "");
  const [fullName, setFullName] = useState(me?.fullName ?? "");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [hasPhoto, setHasPhoto] = useState(Boolean(me?.hasPhoto));

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const next = passwordPairErrors(password, confirm, false);
    setPasswordError(next.password);
    setConfirmError(next.confirm);
    if (next.password || next.confirm) {
      return;
    }

    setError(null);
    try {
      const saved = await endpoints.updateProfile({
        displayName,
        fullName: fullName.trim() || null,
        password: password || null
      });
      setPassword("");
      setConfirm("");
      setHasPhoto(saved.hasPhoto);
      setNotice(password ? "Profile and password updated." : "Profile updated.");
      await refreshMe();
    } catch (err) {
      const apiError = err as ApiError;
      if (apiError.field === "password") {
        setPasswordError(apiError.message);
      } else {
        setError(apiError.message ?? "Save failed.");
      }
    }
  }

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <h1>My profile</h1>
          <p className="page-kicker">Your name, photo, and password for Deed AI.</p>
        </div>
      </div>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      <form className="panel compact-form" onSubmit={onSubmit}>
        <PhotoEditor
          userId={me?.id}
          name={displayName || me?.email}
          email={me?.email}
          hasPhoto={hasPhoto}
          revision={revision}
          error={photoError}
          onUpload={async (file) => {
            setPhotoError(null);
            try {
              const saved = await endpoints.uploadMyPhoto(file);
              setHasPhoto(saved.hasPhoto);
              setRevision((value) => value + 1);
              setNotice("Photo updated.");
              await refreshMe();
            } catch (err) {
              setPhotoError(err instanceof Error ? err.message : "Photo upload failed.");
            }
          }}
          onClear={async () => {
            setPhotoError(null);
            try {
              const saved = await endpoints.clearMyPhoto();
              setHasPhoto(saved.hasPhoto);
              setRevision((value) => value + 1);
              setNotice("Photo removed.");
              await refreshMe();
            } catch (err) {
              setPhotoError(err instanceof Error ? err.message : "Could not remove photo.");
            }
          }}
        />
        <div className="form-grid">
          <label>
            Display name
            <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
          </label>
          <label>
            Full name
            <input value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </label>
          <PasswordPair
            id="profile-password"
            passwordLabel="New password (optional)"
            password={password}
            confirm={confirm}
            passwordError={passwordError}
            confirmError={confirmError}
            onPassword={(value) => {
              setPassword(value);
              if (passwordError) setPasswordError(null);
            }}
            onConfirm={(value) => {
              setConfirm(value);
              if (confirmError) setConfirmError(null);
            }}
          />
        </div>
        <div className="row-actions">
          <button className="primary" type="submit">
            Save
          </button>
        </div>
      </form>
    </section>
  );
}
