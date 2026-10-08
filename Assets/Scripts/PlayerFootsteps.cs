using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
	[Header("Audio")]
	public AudioSource audioSource;
	public AudioClip[] footstepClips;

	[Header("Settings")]
	[Range(0f, 1f)]
	public float volume = 0.5f;

	public void OnFootstep()
	{
		if (audioSource == null)
			return;

		if (footstepClips == null || footstepClips.Length == 0)
			return;

		int randomIndex =
			Random.Range(0, footstepClips.Length);

		AudioClip selectedClip =
			footstepClips[randomIndex];

		audioSource.PlayOneShot(
			selectedClip,
			volume
		);
	}
}