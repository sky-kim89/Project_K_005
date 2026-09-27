using UnityEngine;

/// <summary>
/// 씬 전환 시 오브젝트가 파괴되지 않도록 DontDestroyOnLoad 를 적용하는 컴포넌트.
/// 루트 오브젝트에만 배치할 것 (Unity 제한).
/// </summary>
public class DontDestroyOnLoad : MonoBehaviour
{
    [Tooltip("같은 이름의 루트가 이미 살아 있으면 새로 로드된 쪽을 지운다 (씬을 오가도 하나만 남김).")]
    public bool Unique;
    static readonly System.Collections.Generic.HashSet<string> kept = new System.Collections.Generic.HashSet<string>();

    void Awake()
    {
        if (transform.parent != null)
        {
            Debug.LogWarning($"[DontDestroyOnLoad] '{name}' 은 루트 오브젝트여야 합니다.", this);
            return;
        }
        if (Unique && !kept.Add(name))
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Unique && gameObject.scene.name == "DontDestroyOnLoad") kept.Remove(name);
    }
}
