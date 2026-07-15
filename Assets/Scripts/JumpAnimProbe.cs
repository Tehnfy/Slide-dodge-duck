using System.Reflection;
using System.Text;
using UnityEngine;

// TEMPORARY diagnostic - remove after the standing-jump animation bug is fixed.
// Logs the Animator's real per-frame behavior to a file so it can be inspected
// after a play session.
public class JumpAnimProbe : MonoBehaviour
{
    private const string OutPath = @"C:\Users\Andrejs\AppData\Local\Temp\claude\C--Users-Andrejs-Coursework-Slide-dodge-duck\8e2c723e-88fd-4500-8a4b-9b8e8ee4f532\scratchpad\jump_probe.log";

    private static readonly string[] StateNames =
        { "Idle", "Walk", "Sprint", "Jump", "falling", "Crouch", "Crouch_walk", "Slide" };

    private Animator anim;
    private Movement movement;
    private CharacterController cc;
    private FieldInfo groundedField;
    private FieldInfo vVelField;
    private FieldInfo isJumpingField;
    private FieldInfo wasGroundedField;
    private readonly StringBuilder log = new StringBuilder();
    private int framesSinceFlush;

    private void Start()
    {
        anim = GetComponent<Animator>();
        movement = GetComponent<Movement>();
        cc = GetComponent<CharacterController>();
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        groundedField = typeof(Movement).GetField("Grounded", bf);
        vVelField = typeof(Movement).GetField("verticalVelocity", bf);
        isJumpingField = typeof(Movement).GetField("isJumping", bf);
        wasGroundedField = typeof(Movement).GetField("wasGrounded", bf);
        System.IO.File.WriteAllText(OutPath, "=== probe session start ===\n");
    }

    private string StateName(AnimatorStateInfo info)
    {
        foreach (var n in StateNames)
            if (info.IsName(n)) return n;
        return "?";
    }

    private string ClipsOf(AnimatorClipInfo[] clips)
    {
        if (clips.Length == 0) return "(none)";
        var sb = new StringBuilder();
        foreach (var c in clips)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.AppendFormat("{0}:{1:F2}", c.clip != null ? c.clip.name : "null", c.weight);
        }
        return sb.ToString();
    }

    private void LateUpdate()
    {
        if (anim == null) return;

        var cur = anim.GetCurrentAnimatorStateInfo(0);
        bool inTrans = anim.IsInTransition(0);
        string next = inTrans ? StateName(anim.GetNextAnimatorStateInfo(0)) : "-";
        string curClips = ClipsOf(anim.GetCurrentAnimatorClipInfo(0));
        string nextClips = inTrans ? ClipsOf(anim.GetNextAnimatorClipInfo(0)) : "-";

        bool mGrounded = movement != null && (bool)groundedField.GetValue(movement);
        float mVVel = movement != null ? (float)vVelField.GetValue(movement) : 0f;
        bool mIsJumping = movement != null && (bool)isJumpingField.GetValue(movement);
        bool mWasGrounded = movement != null && (bool)wasGroundedField.GetValue(movement);

        float ty = transform.position.y;
        float ccH = cc != null ? cc.height : -1f;
        float ccC = cc != null ? cc.center.y : -1f;
        float capBottom = cc != null ? ty + ccC - ccH / 2f : -999f;
        bool ccGrounded = cc != null && cc.isGrounded;

        log.AppendFormat(
            "f{0} t={1:F3}: cur={2}({3:F2}) next={4} | animGrounded={5} yVel={6:F2} | mvGrounded={7} wasG={8} vVel={9:F2} jmp={10} | ty={11:F3} ccH={12:F2} ccC={13:F2} capBot={14:F3} ccG={15}\n",
            Time.frameCount, Time.time,
            StateName(cur), cur.normalizedTime, next,
            anim.GetBool("Grounded"), anim.GetFloat("yVelocity"),
            mGrounded, mWasGrounded, mVVel, mIsJumping,
            ty, ccH, ccC, capBottom, ccGrounded);

        if (++framesSinceFlush >= 30)
        {
            Flush();
        }
    }

    private void Flush()
    {
        if (log.Length == 0) return;
        System.IO.File.AppendAllText(OutPath, log.ToString());
        log.Length = 0;
        framesSinceFlush = 0;
    }

    private void OnDisable()
    {
        Flush();
    }
}
