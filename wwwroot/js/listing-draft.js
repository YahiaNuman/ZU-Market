(() => {
  const photos = document.getElementById("Photos");
  const details = document.querySelector("[data-listing-details]");
  if (!photos || !details) return;

  const MAX_PHOTOS = 3;
  const MAX_SIDE = 1600;
  const box = photos.closest(".upload-box");
  const form = photos.closest("form");
  const root = document.querySelector("[data-ai-draft-root]");
  const button = root?.querySelector("[data-ai-draft]");
  const status = root?.querySelector("[data-ai-draft-status]");
  const spinner = root?.querySelector("[data-ai-draft-spinner]");
  const title = document.getElementById("Title");
  const description = document.getElementById("Description");
  const category = document.getElementById("listing-category");
  const condition = document.getElementById("Condition");

  // A page returned after a failed submit already has its details open, so nothing runs by itself.
  let attempted = !details.hidden;
  let busy = false;
  if (button) button.hidden = !attempted;

  const markSuggested = field => {
    field.classList.add("ai-suggested");
    const clear = () => field.classList.remove("ai-suggested");
    field.addEventListener("input", clear, { once: true });
    field.addEventListener("change", clear, { once: true });
  };

  // Whatever the student has already entered is never replaced, and a value the form cannot hold stays empty.
  const fillIfEmpty = (field, value) => {
    if (field.value.trim() !== "" || !value) return;
    field.value = value;
    if (field.value !== value) {
      field.value = "";
      return;
    }
    markSuggested(field);
  };

  // Redrawing on a canvas shrinks the upload and drops hidden photo data such as GPS location.
  const shrink = async file => {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext("2d").drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    return new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error("encode")), "image/jpeg", 0.85));
  };

  const messageFor = httpStatus => {
    if (httpStatus === 429) return root.dataset.limit;
    if (httpStatus === 422) return root.dataset.notRecognized;
    return root.dataset.failed;
  };

  const runDraft = async () => {
    const files = Array.from(photos.files || []).slice(0, MAX_PHOTOS);
    if (busy || files.length === 0) return;

    busy = true;
    attempted = true;
    button.hidden = true;
    spinner.hidden = false;
    status.textContent = root.dataset.working;
    try {
      const data = new FormData();
      data.append("__RequestVerificationToken", form.querySelector("input[name=__RequestVerificationToken]").value);
      for (const file of files) data.append("photos", await shrink(file), "photo.jpg");

      const response = await fetch(root.dataset.url, { method: "POST", body: data });
      if (!response.ok) {
        status.textContent = messageFor(response.status);
        return;
      }

      const draft = await response.json();
      fillIfEmpty(title, draft.title);
      fillIfEmpty(description, draft.description);
      fillIfEmpty(category, String(draft.categoryId));
      fillIfEmpty(condition, String(draft.condition));
      status.textContent = root.dataset.done;
    } catch {
      status.textContent = root.dataset.failed;
    } finally {
      busy = false;
      spinner.hidden = true;
      button.hidden = false;
    }
  };

  photos.addEventListener("photos-changed", () => {
    if (!photos.files?.length) return;
    details.hidden = false;
    box.classList.add("has-photos");
    if (root && !attempted) runDraft();
  });

  button?.addEventListener("click", runDraft);

  box.addEventListener("dragover", event => {
    event.preventDefault();
    box.classList.add("is-dragging");
  });
  box.addEventListener("dragleave", () => box.classList.remove("is-dragging"));
  box.addEventListener("drop", event => {
    event.preventDefault();
    box.classList.remove("is-dragging");
    if (!event.dataTransfer?.files.length) return;
    photos.files = event.dataTransfer.files;
    photos.dispatchEvent(new Event("change"));
  });
})();
