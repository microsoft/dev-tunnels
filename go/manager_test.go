package tunnels

import (
	"context"
	"fmt"
	"log"
	"net/url"
	"os"
	"testing"
)

func getAccessToken() string {
	return "Bearer eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJ4NXQiOiJ4aENLaVp5d1JsUi1LRkl6cVZVcC1HZUNQb1kiLCJ6aXAiOiJERUYifQ.jI5UY7WiYpldnsSkwYQestmewllwSmhDGXGKlL4a0FRi1esv2T-FSE0FSvwBtae_KYCuINg-3Yn6vxi1BQsmoe6uSTDWzfbVXTd5r8ZDxwngU9JPKogjI2UpadEKugFhbIdJKhAwLVJx-aW6Z_CQoZacwwKVC3AW6Hl7IbQN8M6rpOPu3uNkDXqAMjyP7pVp00se8MtvJQFIH6YWv8LjAI8zTP7w_OypHIU6TSj0Bhwl-xhGSVcrdxzaN0QUTwlxrwSqyzY4fTfFdW2h1P3VMUr6AJKAI5LGtLRuhU0w4e4yhKvGvnWBLxW0c1XpHxhZstL1wCzBzydRqFtdpAtdng.MIVTF9DNR06jupPP5-z9oQ.wEBdJyK8B0R785tAXaEf4vFAQLsPDvVZjtFHej5HCZ-r-xBGa6BDKU8gxf3XfM1i1WA3Xakt0-PTQdXaMaXiSXdJEPUGMKQu5bc3lKV3-ZfBfgyvgvx-LOLLYTt5TTsjc4DoJaqK4pugqF-ZJPauhTCQ8Il6sA525d2i-taiQ4C-u9M_KH5ALBD3rF1eprEXhKEqpauRv_yZ2r1w5D74fNJgc8mUUZQlY4njfZOyTOiqFi6n5j_h55SOkjsivjbZ_vpoRlwhCnA78nkrjMmGBOeCnpAiH_X9wVpk5uzKhh9voMF-t8LAtDvbFZnvEm9snLC3QLRS7HTOwkmKpemGRy3CcmUrmC1_TecNwQ1gFQxD8pgfSQHKSDAfmSepb5ltkKrtPBjmhg4XIyUAdlWb3eJM6O9hWHpWm7EBqvtwKME-NIgT9falcP8aa9gtHN14lseyVaX9_V6h3gFLpZU7HIoun_odzI9G9XFH4Pc3XHIVwvfmHZVNQGlIxeoj-Jj1CsWaw4GJMN3wrX3WS7CcCsTAZY_PrlPw3aC8tr3DXyA0P_pRn5I2r0I_Hzzqe0h7C4UFpQmYDWCj_tqpejxoKNUZyLZ4-CtkG6HeKsdeqh-5UL_q_hgIqETgOQgGei0_neTmaCKuMkkCgoKxxpXYx3unXK7__ziNOtgASW8_CSROWbrILzUiTLDuLJeA5zWNyTlf9fC-99bvZ-0CVKukQYl8C0fu5ia1tzJrPQdBIw8GHW-hQbVNDfTCJsY6havy921cND2vVcwB1ZDO1laDAnBdRgUtr6KB9qXjaIXXSs3DwF1x8kxNMC8Lp9QE-jKqZKB75SxpFGHM0D9235WwhHDVKwQs08GLPGBeq60X6e8laceB9YWwwwAJeEWXhlA1q3hTBlg8IFcBWGQw5wDwp_AIrJqFDRIZO7oSXk1Z5A4X-qpzFIhlD059062g4P9Su5-wqcBBPCcqkVsEy-_XaTDULZUogd_EMjPhjPHWRF7RTKi77tolwoSHNekDdk9jjPn1-jUG4DmL41hl_V4P2ISgwBNaXLF7JifJ044Isq1MduGjB5ijo1Oom4kjh8rpUJxmcNhj6mKw8jvsj4p9rHcybry4EGO6sRb5nzClaiA_9mWskETwBzStLEk9QmjilhhA7WUKKxXuQJIg_IgKGsOclfoMjpr8mn6bY5CCbfZ9HV37uQdGi-5k-GN8cpZuO-_BNAR9k154tM3o2BdvNHO4IvncBq5Y1HR_M360ano2J2aeRfkxXdSbWxtlYCf0ztGePLo_OFKr6Xfeulp63zpYoXbTBcvwhzzTjaCWq1Blm5Gcg3ZaO4xfwo5NdqwdRUA7KtiAErlk2aCM_uuV8HbDSmQSLm1KGopQcMf2BF_tEXpEYdZdG5pCXsbq6cwweDTGyA7Tl_pKTfAW0IIA65u2K1L2zwJqIJJEYrvJj1K0tTrjpxzB1OYnH7U3ulL3LoRPy-WNzljiSNJyjg_Ns2PWx3aBIKRw4bPr31UyIZz42vzIATrtT6qm-ZS0645mdSr2BQurs3_ShI6bdCO6eSgUtxTejlPNS7OtQdqt46o1hz7zh_ow4f3QHuxTJWInXsagl8uKewQQbxFVY8gzJVCoTFZqUJv1BdG70Bly_sT3pNJFzaXJpG20_h5HRvkB560I6mhF1cX0ELDzGLCBUctFZxOA3p1blzSNofd7v-nWGNYCDhVdkTGINQqn4BRt.Ilo_t5jcUCJD3jlB8FmlQg"
}

func TestTunnelCreate(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse("https://global.rel.tunnels.api.visualstudio.com/")
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
		createdTunnel.table().Print()
	}
}

func TestListTunnels(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse("https://global.rel.tunnels.api.visualstudio.com/")
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	options := &TunnelRequestOptions{}
	tunnels, err := managementClient.ListTunnels(context.Background(), "", "", options)
	if err != nil {
		t.Errorf(err.Error())
	}
	for _, tunnel := range tunnels {
		logger.Println(fmt.Sprintf("found tunnel with id %s", tunnel.TunnelID))
		tunnel.table().Print()
	}

}

func TestTunnelCreateAndGet(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse("https://global.rel.tunnels.api.visualstudio.com/")
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
	}

	getTunnel, err := managementClient.GetTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}
}

func TestTunnelCreateGetDelete(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse("https://global.rel.tunnels.api.visualstudio.com/")
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
	}

	getTunnel, err := managementClient.GetTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}

	tunnelDeleted, err := managementClient.DeleteTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
	}
	if !tunnelDeleted {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", getTunnel.TunnelID))
	}
}
